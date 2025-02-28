namespace App.WindowsService;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using log4net;
using Microsoft.Extensions.Options;

/// <summary>
/// Manages the expiration of cache items based on their TTL.
/// Uses a sorted dictionary to group cache items by their TTLs.
/// Insertion time is O(1), expiry check is O(log n), and removal is O(log n).
/// Clubs TTLs into time buckets to reduce the number of checks.
/// </summary>
public sealed class ExpiryManager
{
    private static readonly ILog log = LogManager.GetLogger(typeof(ExpiryManager));
    //private readonly ConcurrentDictionary<string, CacheItem> _cache; // Reference to main cache

    private readonly CacheSettings _cacheSettings;
    private readonly CacheManagerCore _cacheManagerCore;
    private readonly SortedDictionary<long, HashSet<CacheItem>> _expiryMap;
    private readonly object _lock = new object();
    private readonly int _monitoringIntervalInSecs; // Monitoring thread interval in seconds
    private readonly int _expiryOffset;   // Offset window in seconds
    private bool _isRunning = true;

    private bool _strictExpiry = false;

    public ExpiryManager(IOptions<CacheSettings> cacheSettings, CacheManagerCore cacheManagerCore, int expiryInterval = 6)
    {
        //_cache = cache;
        _cacheManagerCore = cacheManagerCore;
        _expiryMap = new SortedDictionary<long, HashSet<CacheItem>>();

        _monitoringIntervalInSecs = expiryInterval;
        _strictExpiry = cacheSettings.Value.StrictExpiry;
        _expiryOffset = expiryInterval / 2; // ±3 sec if expiryInterval = 6 sec

        _cacheManagerCore.CreateEvent += (sender, args) => AddItem((CacheItem)args.Item);
        _cacheManagerCore.UpdateEvent += (sender, args) => UpdateItem((CacheCoreEventArgs)args);
        _cacheManagerCore.DeleteEvent += (sender, args) => RemoveItem((CacheItem)((CacheCoreEventArgs)args).Item);

        StartExpiryThread();
    }

    /// <summary>
    /// Adds a cache item to the expiration dictionary, grouping TTLs into time buckets.
    /// </summary>
    public void AddItem(CacheItem item)
    {
        long roundedTTL = GetRoundedTTL(item.TTL); // Getting the nearest expiry bucket so that we can group items by their rounded TTL

        lock (_lock)
        {
            if (!_expiryMap.TryGetValue(roundedTTL, out var set))
            {
                // Create a new bucket if it doesn't exist
                set = new HashSet<CacheItem>();
                _expiryMap[roundedTTL] = set;
            }
            // Add to an existing expiry bucket
            set.Add(item); // HashSet prevents duplicate entries automatically
        }
    }

    public void UpdateItem(CacheCoreEventArgs args)
    {
        CacheItem oldItem = args.OldItem;
        CacheItem newItem = args.Item;
        if (oldItem.TTL == newItem.TTL)
        {
            log.Debug($"ExpiryManager: UpdateItem: TTL not changed for {newItem.Key}, old TTL: {oldItem.TTL}, new TTL: {newItem.TTL}");
            return;
        }
        log.Debug($"ExpiryManager: UpdateItem: TTL changed for {newItem.Key}, old TTL: {oldItem.TTL}, new TTL: {newItem.TTL}");
        // Todo: Should add seperate logic for updating the expiry map?
        RemoveItem(args.Item);
        AddItem(args.Item);

    }

    /// <summary>
    /// Rounds a given TTL to the nearest expiry bucket based on `_monitoringIntervalInSecs`.
    /// Will always return the same value for the same TTL.
    /// </summary>
    private long GetRoundedTTL(long ttl)
    {
        return (ttl / _monitoringIntervalInSecs) * _monitoringIntervalInSecs;
    }

    /// <summary>
    /// Periodically checks and removes expired cache items.
    /// </summary>
    private void StartExpiryThread()
    {
        Thread expiryThread = new Thread(() =>
        {
            while (_isRunning)
            {
                Thread.Sleep(_monitoringIntervalInSecs * 1000); // Sleep for configured interval
                ExpireItems();
            }
        })
        {
            IsBackground = true
        };
        expiryThread.Start();
    }

    /// <summary>
    /// Removes a cache item from _expiryMap, to be called after removing from the main cache.
    /// </summary>
    public void RemoveItem(CacheItem item)
    {
        long roundedTTL = GetRoundedTTL(item.TTL); // Will always return the same value for the same TTL

        lock (_lock)
        {
            if (_expiryMap.TryGetValue(roundedTTL, out var items))
            {
                items.Remove(item);  // Remove from HashSet
                if (items.Count == 0)
                {
                    _expiryMap.Remove(roundedTTL);  // Remove empty bucket, for freeing up memory
                }
            }
        }
    }

    /// <summary>
    /// Checks for expired items and removes them from cache
    /// </summary>
    public void ExpireItems()
    {
        long currentTime = GetCurrentTimestamp();
        long upperBound = currentTime + _expiryOffset;

        // Track keys of empty TTL buckets in _expiryMap
        // Need to remove them since they are expired and empty
        // No new insertions can and will be made in these buckets
        List<long> emptyBucketKeysToRemoveFromExpiryMap = new();
        List<string> keysToRemoveFromMainCache = new();
        log.Debug($"ExpiryManager: Checking for expired items at {currentTime}");

        List<long> keysInExpiryMapLessThanUpperBound = new List<long>();

        List<CacheItem> expiredItems = new List<CacheItem>();

        // Copy keys that are less than upperBound to a separate list to minimize lock contention
        lock (_lock)
        {
            foreach (long ttl in _expiryMap.Keys)
            {
                if (ttl <= upperBound)
                {
                    keysInExpiryMapLessThanUpperBound.Add(ttl);  // Add to list if the ttl is less than upperBound
                }
                else
                {
                    break;  // Stop if TTL is beyond offset range, No need to iterate through all the keys
                }
            }
        }

        // Loop through the keys that are less than upperBound
        foreach (var ttl in keysInExpiryMapLessThanUpperBound)
        {
            HashSet<CacheItem> itemSet;
            lock (_lock)
            {
                itemSet = _expiryMap[ttl]; // the hashset against the rounded TTL bucket
                                           // if(ttl > upperBount) break; // No need to check ttl again, since we are already iterating over keys less than upperBound
            }
            // Remove from cache and expiry map
            foreach (var expiredItem in itemSet.ToList())
            {
                //_cacheManagerCore.Delete(expiredItem.Key); // Not removing from main cache here, since we are inside a lock, can cause deadlock
                expiredItem.IsExpired = true;   // Will only be used if strict expiry is false, otherwise it will be expired based on ttl
                itemSet.Remove(expiredItem);
                expiredItems.Add(expiredItem);
                keysToRemoveFromMainCache.Add(expiredItem.Key); // These are they keys which we have to remove from the main cache, cannot remove inside lock
            }
            // Need to lock here as well?
            if (itemSet.Count == 0)
                emptyBucketKeysToRemoveFromExpiryMap.Add(ttl); // These are empty TTL buckets
        }

        // Removing items from main cache outside lock to prevent deadlocks
        if (_strictExpiry)
        {
            foreach (var key in expiredItems)
            {
                _cacheManagerCore.Delete(key.Key); 
            }
        }
        else
        {
            foreach (var item in expiredItems)
            {
                _cacheManagerCore.Update(item);
            }
        }

        lock (_lock)
        {
            // Clean up empty TTL buckets
            foreach (var key in emptyBucketKeysToRemoveFromExpiryMap)
            {
                // Need to check null again, since key might contain a value now, since lock was released after getting the keys
                //if(_expiryMap.ContainsKey(key) && _expiryMap[key].Count == 0)
                // No need to check for empty TTL bucket, because a new entry cannot be added to an old TTL bucket
                _expiryMap.Remove(key);
            }
        }
    }

    /// <summary>
    /// Gets the current timestamp (Unix time in seconds).
    /// </summary>
    private long GetCurrentTimestamp()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    /// <summary>
    /// Stops the expiry monitoring thread.
    /// </summary>
    public void Stop()
    {
        _isRunning = false;
    }
}
