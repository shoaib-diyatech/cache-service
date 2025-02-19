using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using log4net;

/// <summary>
/// Manages the expiration of cache items based on their TTL.
/// Uses a sorted dictionary to group cache items by their TTLs.
/// Insertion time is O(1), expiry check is O(log n), and removal is O(log n).
/// Clubs TTLs into time buckets to reduce the number of checks.
/// </summary>
public sealed class ExpiryManager
{
    private static readonly ILog log = LogManager.GetLogger(typeof(ExpiryManager));
    private readonly ConcurrentDictionary<string, CacheItem> _cache; // Reference to main cache
    private readonly SortedDictionary<long, HashSet<CacheItem>> _expiryMap;
    private readonly object _lock = new object();
    private readonly int _expiryInterval; // Monitoring thread interval in seconds
    private readonly int _expiryOffset;   // Offset window in seconds
    private bool _isRunning = true;

    public ExpiryManager(ConcurrentDictionary<string, CacheItem> cache, int expiryInterval = 6)
    {
        _cache = cache;
        _expiryMap = new SortedDictionary<long, HashSet<CacheItem>>();

        _expiryInterval = expiryInterval;
        _expiryOffset = expiryInterval / 2; // ±3 sec if expiryInterval = 6 sec

        StartExpiryThread();
    }

    /// <summary>
    /// Adds a cache item to the expiration dictionary, grouping TTLs into time buckets.
    /// </summary>
    public void Add(CacheItem item)
    {
        long roundedTTL = GetRoundedTTL(item.TTL);

        lock (_lock)
        {
            if (!_expiryMap.TryGetValue(roundedTTL, out var set))
            {
                set = new HashSet<CacheItem>();
                _expiryMap[roundedTTL] = set;
            }
            set.Add(item); // HashSet prevents duplicate entries automatically
        }
    }


    /// <summary>
    /// Rounds a given TTL to the nearest expiry bucket based on `_expiryOffset`.
    /// </summary>
    private long GetRoundedTTL(long ttl)
    {
        return (ttl / _expiryInterval) * _expiryInterval;
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
                ExpireItems();
                Thread.Sleep(_expiryInterval * 1000); // Sleep for configured interval
            }
        })
        {
            IsBackground = true
        };
        expiryThread.Start();
    }

    public void RemoveItem(CacheItem item)
    {
        long ttl = GetRoundedTTL(item.TTL);

        long roundedTTL = GetRoundedTTL(item.TTL);

        if (_expiryMap.TryGetValue(roundedTTL, out var items))
        {
            items.Remove(item);  // Remove from HashSet
            if (items.Count == 0)
            {
                _expiryMap.Remove(ttl);  // Remove empty bucket
            }
        }
    }

    /// <summary>
    /// Checks for expired items and removes them from cache in batch.
    /// </summary>
    public void ExpireItems()
    {
        long currentTime = GetCurrentTimestamp();
        long lowerBound = currentTime - _expiryOffset;
        long upperBound = currentTime + _expiryOffset;
        List<long> keysToRemove = new();

        lock (_lock)
        {
            foreach (var (ttl, itemSet) in _expiryMap)
            {
                if (ttl > upperBound)
                    break; // Stop if TTL is beyond offset range

                // Collect expired items to remove
                var expiredItems = itemSet
                    .Where(item => item.TTL <= currentTime)
                    .ToList(); // ToList prevents modifying collection while iterating

                // Remove from cache and expiry map
                foreach (var expiredItem in expiredItems)
                {
                    _cache.TryRemove(expiredItem.Key, out _);
                    itemSet.Remove(expiredItem);
                }

                if (itemSet.Count == 0)
                    keysToRemove.Add(ttl);
            }

            // Clean up empty TTL buckets
            foreach (var key in keysToRemove)
                _expiryMap.Remove(key);
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
