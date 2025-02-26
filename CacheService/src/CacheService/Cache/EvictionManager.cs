namespace App.WindowsService;

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.ConstrainedExecution;
using Microsoft.Extensions.Options;
using log4net;

/// <summary>
/// Evicts based on LFU (Least Frequent Used) policy.
/// Keeps track of usage counts for each key and evicts the least used key when OnEvictionNeeded is raised.
/// </summary>
public class EvictionManager
{
    private static readonly ILog log = LogManager.GetLogger(typeof(EvictionManager));
    /// <summary>
    /// Maps the usage frequency to the cache items.
    /// Key being the usage frequency and value being the Dictornary of cache items, with key as the cache key.
    /// </summary>
    private readonly Dictionary<int, Dictionary<string, CacheItem>> _usageFequency;

    /// <summary>
    /// Keeps a sorted count of usage frequency.
    /// Used to get the least used item.
    /// </summary>
    private readonly Dictionary<int, int> _frequencyListMapCount;
    object _frequencyCountLock;
    object _lock;

    private readonly CacheManagerCore _cacheManagerCore;

    private const float EvictionFactor = 0.75f;

    private readonly CacheSettings _cacheSettings;

    private void IncrementFrequencyList(int frequency)
    {
        lock (_frequencyCountLock)
        {
            if (_frequencyListMapCount.ContainsKey(frequency))
            {
                _frequencyListMapCount[frequency]++;
            }
            else
            {
                _frequencyListMapCount.Add(frequency, 1);
            }
        }
        // Use Interlocked to update smallestFrequency
        Interlocked.Exchange(ref smallestFrequency, Math.Min(Interlocked.CompareExchange(ref smallestFrequency, frequency, smallestFrequency), frequency));

        // Use Interlocked to update highestFrequency
        Interlocked.Exchange(ref highestFrequency, Math.Max(Interlocked.CompareExchange(ref highestFrequency, frequency, highestFrequency), frequency));        
    }
    private void DecrementFrequencyList(int frequency)
    {
        lock (_frequencyCountLock)
        {
            if (_frequencyListMapCount.ContainsKey(frequency))
            {
                _frequencyListMapCount[frequency]--;
                if (_frequencyListMapCount[frequency] == 0)
                {
                    _frequencyListMapCount.Remove(frequency);
                }
            }
        }
        
        // Use Interlocked to update smallestFrequency
        Interlocked.Exchange(ref smallestFrequency, Math.Min(Interlocked.CompareExchange(ref smallestFrequency, frequency, smallestFrequency), frequency));

        // Use Interlocked to update highestFrequency
        Interlocked.Exchange(ref highestFrequency, Math.Max(Interlocked.CompareExchange(ref highestFrequency, frequency, highestFrequency), frequency));
    }


    /// <summary>
    /// The smallest frequency in the <see cref="_usageFrequency"/> 
    /// </summary>
    private int smallestFrequency = int.MaxValue;

    /// <summary>
    /// The highest frequency in the <see cref="_usageFrequency"/>
    /// </summary>
    private int highestFrequency = int.MinValue;

    /// <summary>
    /// Total number of items in <see cref="_usageFequency"/>
    /// </summary>
    private int _usageFequencyTotalItems;
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
        Interlocked.Increment(ref _usageFequencyTotalItems);
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
        bool incrementedSuccessfully = false;
        bool oldFrequencyExists = false;
        lock (_lock)
        {
            if (_usageFequency.ContainsKey(currentUsageCount))
            {
                items = _usageFequency[currentUsageCount];
                // Removing item from old frequency
                items.Remove(item.Key);
                oldFrequencyExists = true;
            }
            item.UsageCount++;
            // Check if the new frequency exists in the _usageFrequency
            if (_usageFequency.ContainsKey(item.UsageCount))
            {
                _usageFequency[item.UsageCount].Add(item.Key, item);
            }
            else
            {
                // Creating a new frequency adding the item, with its key as the key
                _usageFequency.Add(item.UsageCount, new() { { item.Key, item } });
            }
            incrementedSuccessfully = true;
        }
        if(incrementedSuccessfully)
        {
            IncrementFrequencyList(item.UsageCount);
        }
        if(oldFrequencyExists)
        {
            DecrementFrequencyList(currentUsageCount);
        }
    }


    private void RemoveItem(EventArgs args)
    {
        Interlocked.Decrement(ref _usageFequencyTotalItems);
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
        // Get the item from the _frequencyListMapCount, it would be _frequencyListMapCount[smallestFrequency]
        // This would be smallest frequency which has items in _usageFrequency
        // Get the relevant Dictioanry from _usageFrequency against this frequency
        // Start removing these items from the cache until the EvictionFactor is reached
        int itemsToEvict = (int)(EvictionFactor * _usageFequencyTotalItems);
        int evictedItems = 0;
        Dictionary<string, CacheItem> items;
        int _usageFrequencyToEvict;
        List<string> keysToRemoveFromCache = new();
        lock (_lock)
        {
            while (evictedItems < itemsToEvict)
            {
                //Get the first item from the _sortedUsageList
                if (_frequencyListMapCount.ContainsKey(smallestFrequency) == false)
                {
                    log.Debug($"EvictionManager: No items to evict, smallestFrequency: {smallestFrequency}");
                    break;
                }
                _usageFrequencyToEvict = _frequencyListMapCount[smallestFrequency];
                items = _usageFequency[_usageFrequencyToEvict];

                // Looping inside the Dictionary to remove items
                foreach (var item in items.ToList())
                {
                    if (evictedItems >= itemsToEvict)
                    {
                        break;
                    }
                    keysToRemoveFromCache.Add(item.Value.Key);

                    // Remove the item from the _usageFrequency
                    items.Remove(item.Key);
                    DecrementFrequencyList(_usageFrequencyToEvict);
                    evictedItems++;
                }
            }
        }

        // Removing items from main cache outside lock to prevent deadlocks
        log.Debug($"EvictionManager: Evicting {evictedItems} items from the cache");
        foreach (var key in keysToRemoveFromCache)
        {
            _cacheManagerCore.Delete(key);
        }
        log.Info($"EvictionManager: Evicted {evictedItems} items from the cache");
    }
}