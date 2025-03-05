namespace App.WindowsService;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using log4net;
using Microsoft.Extensions.Options;
using Microsoft.VisualBasic;

/// <summary>
/// Manages the expiration of cache items based on their TTL.
/// Uses a sorted dictionary to group cache items by their TTLs.
/// Insertion time is O(1), expiry check is O(log n), and removal is O(log n).
/// Clubs TTLs into time buckets to reduce the number of checks.
/// </summary>
public sealed class ExpiryManager
{
    private static readonly ILog log = LogManager.GetLogger(typeof(ExpiryManager));

    private readonly CacheManagerCore _cacheManagerCore;
    //private readonly SortedDictionary<long, HashSet<CacheItem>> _expiryMap;
    private readonly SortedDictionary<long, Dictionary<string, CacheItem>> _expiryMap;

    /// <summary>
    /// Maps the CacheItem id with TTL bucket, i.e. id of <see cref="_expiryMap"/>
    /// </summary>
    private readonly Dictionary<string, long> _idExpiryMap;

    private readonly object _lock = new object();

    private readonly object _idExpiryMapLock = new object();
    private readonly int _monitoringIntervalInSecs; // Monitoring thread interval in seconds
    private readonly int _expiryOffset;   // Offset window in seconds
    private bool _isRunning = true;

    private bool _strictExpiry = false;
    private bool _slidingExpiry = true;

    private long currentCounter = 0;

    public ExpiryManager(IOptions<CacheSettings> cacheSettings, CacheManagerCore cacheManagerCore, int expiryInterval = 6)
    {
        _cacheManagerCore = cacheManagerCore;
        _expiryMap = new();
        _idExpiryMap = new();

        _monitoringIntervalInSecs = expiryInterval;
        _strictExpiry = cacheSettings.Value.StrictExpiry;
        _slidingExpiry = cacheSettings.Value.SlidingExpiry;
        _expiryOffset = expiryInterval / 2; // ±3 sec if expiryInterval = 6 sec

        _cacheManagerCore.CreateEvent += (sender, args) => AddItem((CacheItem)args.Item);
        _cacheManagerCore.UpdateEvent += (sender, args) => UpdateItem((CacheCoreEventArgs)args);
        _cacheManagerCore.ReadEvent += (sender, args) => ReadItem(((CacheCoreEventArgs)args).Item);
        _cacheManagerCore.DeleteEvent += (sender, args) => RemoveItem(((CacheCoreEventArgs)args).Item);

        StartExpiryThread();
    }

    /// <summary>
    /// Adds a cache item to the expiration dictionary, grouping TTLs into time buckets.
    /// </summary>
    public void AddItem(CacheItem item)
    {
        if (item.TTL == 0)
        {
            return; // If TTL is 0 Expiry doesn't work
        }
        long roundedTTL = GetRoundedTTL(item.TTL); // Getting the nearest expiry bucket so that we can group items by their rounded TTL
        Dictionary<string, CacheItem> dict = null;
        lock (_lock)
        {
            if (!_expiryMap.TryGetValue(roundedTTL, out dict))
            {
                // Create a new bucket if it doesn't exist
                dict = new Dictionary<string, CacheItem>();
                _expiryMap[roundedTTL] = dict;
            }
            // Add to an existing expiry bucket
            //dict.Add(item.Key, item); // Added a granular lock on 'dict' to avoid nested locking
        }
        lock (dict)
        {
            dict.Add(item.Key, item);
        }
        lock (_idExpiryMapLock)
        {
            _idExpiryMap.Add(item.Key, roundedTTL); // Adding the bucket against the id to be fetched later.
        }
    }

    /// <summary>
    /// Removes a cache item from _expiryMap, to be called after removing from the main cache.
    /// </summary>
    public void RemoveItem(CacheItem item)
    {
        bool canRemoveFromDict = false;
        Dictionary<string, CacheItem> dict = null;
        long TTL = 0;
        bool removedFromIdExpiryMap = false;
        lock (_idExpiryMapLock)
        {
            if (_idExpiryMap.Remove(item.Key, out TTL))
            {
                removedFromIdExpiryMap = true;
            }
        }
        if (!removedFromIdExpiryMap)
        {
            return;
        }
        lock (_lock)
        {
            if (_expiryMap.TryGetValue(TTL, out dict))
            {
                //dict.Remove(item.Key);
                canRemoveFromDict = true;
            }
        }
        if (canRemoveFromDict)
        {
            if (dict != null)
            {
                lock (dict)
                {
                    dict.Remove(item.Key); // Not checking for empty dictionary here as empty dictionaries are only removed in ExpireItems
                }
            }
        }
    }

    /// <summary>
    /// Reads a cache item, updating its TTL if sliding expiry is enabled.
    /// </summary>
    public void ReadItem(CacheItem item)
    {
        if (_slidingExpiry == false)
        {
            return; // If sliding Expiry is not enabled then no need to update the TTL 
        }
        if (item.TTL == 0)
        {
            return; // If TTL is 0 Expiry doesn't work
        }

        RemoveItem(item);
        AddItem(item);
    }


    public void UpdateItem(CacheCoreEventArgs args)
    {
        CacheItem oldItem = args.OldItem;
        CacheItem newItem = args.Item;
        if (oldItem.TTL == newItem.TTL)
        {
            if (_slidingExpiry == false) // No need to update since sliding expiry is not enabled
            {
                log.Debug($"ExpiryManager: UpdateItem: TTL not changed for {newItem.Key}, old TTL: {oldItem.TTL}, new TTL: {newItem.TTL}");
                return;
            }
        }
        if (newItem.TTL == 0)
        {
            RemoveItem(newItem);
            return;
        }

        log.Debug($"ExpiryManager: UpdateItem: TTL changed for {newItem.Key}, old TTL: {oldItem.TTL}, new TTL: {newItem.TTL}");
        // Todo: Should add seperate logic for updating the expiry map?
        RemoveItem(args.Item);
        AddItem(args.Item);
    }

    /// <summary>
    /// Rounds a given TTL to the nearest expiry bucket based on `_monitoringIntervalInSecs` and CurrentTimestamp from <see cref="GetCurrentTimestamp"/>.
    /// </summary>
    private long GetRoundedTTL(long ttl)
    {
        return ((GetCurrentTimestamp() + ttl) / _monitoringIntervalInSecs) * _monitoringIntervalInSecs;
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
                    //Dictionary<string, CacheItem> dict = _expiryMap[ttl];
                    //expiredItems.AddRange(dict.Values);
                }
                else
                {
                    break;  // Stop if TTL is beyond offset range, No need to iterate through all the keys
                }
            }
        }
        // lock(_lock){
        //     foreach (var ttl in keysInExpiryMapLessThanUpperBound)
        //     {
        //         if (_expiryMap.ContainsKey(ttl) && _expiryMap[ttl].Count == 0)
        //         {
        //             _expiryMap.Remove(ttl);
        //         }
        //     }
        // }

        // Loop through the keys that are less than upperBound
        foreach (var ttl in keysInExpiryMapLessThanUpperBound)
        {
            Dictionary<string, CacheItem> dict;
            lock (_lock)
            {
                dict = _expiryMap[ttl];
            }

            var keysToRemove = new List<string>(); // avoiding ToList()
            lock (dict)
            {
                foreach (var (key, expiredItem) in dict)
                {
                    expiredItems.Add(expiredItem);
                    lock(_idExpiryMapLock)
                    {
                        _idExpiryMap.Remove(key);
                    }
                }

                foreach (var key in keysToRemove)
                {
                    dict.Remove(key);
                }

                if (dict.Count == 0)
                {
                    emptyBucketKeysToRemoveFromExpiryMap.Add(ttl); // These are empty TTL buckets
                }
            }
        }

        // Removing items from main cache outside lock to prevent deadlocks

        foreach (var expiredItem in expiredItems)
        {
            if (_strictExpiry)
                _cacheManagerCore.Delete(expiredItem.Key);
            else
                _cacheManagerCore.SetAsExpired(expiredItem);
        }


        lock (_lock)
        {
            // Clean up empty TTL buckets
            foreach (var key in emptyBucketKeysToRemoveFromExpiryMap)
            {
                // No Need to check null again, since key might contain a value now, since lock was released after getting the keys
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
