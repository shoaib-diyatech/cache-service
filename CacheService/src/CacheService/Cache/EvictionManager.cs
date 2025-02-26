namespace App.WindowsService;

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.ConstrainedExecution;
using Microsoft.Extensions.Options;

/// <summary>
/// Evicts based on LFU (Least Frequently Used) policy.
/// Keeps track of usage counts for each key and evicts the least used key when OnEvictionNeeded is raised.
/// </summary>
public class EvictionManager
{
    private readonly Dictionary<int, Dictionary<string, CacheItem>> _usageFequency;

    /// <summary>
    /// Keeps a sorted count of usage frequency.
    /// Used to get the least used item.
    /// </summary>
    private readonly SortedList<int, int> _sortedUsageList;

    object _lock;

    private readonly CacheManagerCore _cacheManagerCore;

    private const float EvictionFactor = 0.75f;

    private readonly CacheSettings _cacheSettings;

    private int TotalItems;
    public EvictionManager(IOptions<CacheSettings> cacheSettings, CacheManagerCore cacheManagerCore)
    {
        _cacheSettings = cacheSettings.Value;
        _usageFequency = new();
        _lock = new object();
        _cacheManagerCore = cacheManagerCore;
        _cacheManagerCore.EvictionNeeded += OnEvictionNeeded;
        _cacheManagerCore.CreateEvent += (sender, args) => AddItem(args);
        _cacheManagerCore.UpdateEvent += (sender, args) => IncrementUsage(args);
        _cacheManagerCore.ReadEvent += (sender, args) => IncrementUsage(args);
        _cacheManagerCore.DeleteEvent += (sender, args) => RemoveItem(args);
    }

    private void AddItem(EventArgs item)
    {
        TotalItems++;
        IncrementUsage(item);
    }

    /// <summary>
    /// Increments the usage count of the cache item.
    /// </summary>
    /// <param name="args"></param>
    private void IncrementUsage(EventArgs args)
    {
        CacheItem item = ((CacheEventArgs)args).Item;
        int currentUsageCount = item.UsageCount;
        Dictionary<string, CacheItem> items;
        lock (_lock)
        {
            if (_usageFequency.ContainsKey(currentUsageCount))
            {
                items = _usageFequency[currentUsageCount];
                items.Remove(item.Key);
            }
            item.UsageCount++;
            if (_usageFequency.ContainsKey(item.UsageCount))
            {
                _usageFequency[item.UsageCount].Add(item.Key, item);
            }
            else
            {
                _usageFequency.Add(item.UsageCount, new() { { item.Key, item } });
            }
        }
    }


    private void RemoveItem(EventArgs args)
    {
        TotalItems--;
        CacheItem item = ((CacheEventArgs)args).Item;
        int currentUsageCount = item.UsageCount;
        Dictionary<string, CacheItem> items;
        lock (_lock)
        {
            if (_usageFequency.ContainsKey(item.UsageCount))
            {
                items = _usageFequency[item.UsageCount];
                items.Remove(item.Key);
            }
        }
    }

    private void OnEvictionNeeded(object? sender, CacheEventArgs e)
    {
        Evict();
    }

    public void Evict()
    {
        //Get the first item from the _sortedUsageList
        //Remove the item from the _usageFequency
        //If _usageFrequency is empty, remove the key from _sortedUsageList
        int itemsToEvict = (int)(EvictionFactor * TotalItems);
        int evictedItems = 0;
        lock (_lock)
        {
            foreach (var usage in _usageFequency)
            {
                foreach (var item in usage.Value)
                {
                    if (evictedItems >= itemsToEvict)
                    {
                        return;
                    }
                    _cacheManagerCore.Delete(item.Value.Key);
                    evictedItems++;
                }
            }
        }
    }
}