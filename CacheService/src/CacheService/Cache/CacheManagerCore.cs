namespace App.WindowsService;

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using log4net;
using Microsoft.Extensions.Options;

/// <summary>
/// Cache Manager class to manage the cache operations, Uses <see cref="ConcurrentDictionary"/> DataStructure for thread safety.
/// Maintains the current memory usage and max memory usage to avoid memory overflow, via <see cref="MemoryManager"/>.
/// </summary>
public class CacheManagerCore
{
    private readonly Dictionary<string, CacheItem> _cache;
    private readonly object _lock = new object();
    private static readonly ILog log = LogManager.GetLogger(typeof(CacheManager));
    private readonly MemoryManager _memoryManager;

    public event EventHandler<CacheCoreEventArgs> CreateEvent;
    public event EventHandler<CacheCoreEventArgs> UpdateEvent;
    public event EventHandler<CacheCoreEventArgs> DeleteEvent;
    public event EventHandler<CacheCoreEventArgs> ReadEvent;
    public event EventHandler FlushAllEvent;
    public event EventHandler EvictionNeeded;

    CacheSettings _cacheSettings;

    public CacheManagerCore(IOptions<CacheSettings> cacheSettings, MemoryManager memoryManager)
    {
        _cacheSettings = cacheSettings.Value;
        //EvictionThreshold = _cacheSettings.EvictionThreshold;
        _cache = new();
        _memoryManager = memoryManager;
    }

    public double GetCurrentMemoryUsageInMB() => _memoryManager.GetCurrentMemoryUsageInMB();

    public bool Create(CacheItem item)
    {
        long size = _memoryManager.GetSizeInBytes(item.Key, item.Value.ToString());

        // if (_memoryManager.CanAdd(size))
        // {
        //     log.Error($"Memory limit reached, cannot add more items to the cache, currentMemoryUsageInBytes: {_memoryManager.CurrentMemoryUsageInBytes}");
        //     OnEvictionNeeded();
        //     return false;
        // }
        if (_memoryManager.EvictionNeeded())
        {
            log.Warn($"CacheManagerCore: Memory threshold reached, Eviction needed, currentMemoryUsageInBytes: {_memoryManager.CurrentMemoryUsageInBytes}");
            OnEvictionNeeded();
        }

        bool isInsertedSuccessfully = false;
        lock (_lock)
        {
            if (_cache.ContainsKey(item.Key) == false)
            {
                _cache.Add(item.Key, item);
                isInsertedSuccessfully = true;
            }
            else
            {
                log.Error($"Key: {item.Key} already exists in the cache, cannot add duplicate key");
                throw new ArgumentException($"Key: {item.Key} already exists in the cache, cannot add duplicate key");
            }
        }

        if (isInsertedSuccessfully)
        {
            _memoryManager.Add(size);
            //OnCreateEvent(item.Key, item.Value.ToString());
            OnCreateEvent(item);
            log.Debug($"Added key: {item.Key}, Value: {item.Value}, currentMemoryUsageInBytes: {_memoryManager.CurrentMemoryUsageInBytes}");
            return true;
        }
        return false;
    }

    /// <summary>
    /// Reads the value of the cache item, if the key does not exist, returns null.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public object Read(string key)
    {
        bool isReadSuccessfully = false;
        CacheItem item = null;
        lock (_lock)
        {
            if (_cacheSettings.StrictExpiry)
            {
                if (_cache.TryGetValue(key, out item) == false)
                {
                    log.Warn($"Key: {key} does not exist in the cache, cannot read");
                    return null;
                }
                // if item exists in _cache but is expired, then remove it from the cache and return null
                if (item.IsExpired)
                {
                    log.Warn($"Key: {key} is expired, cannot read");
                    _cache.Remove(key);
                    _memoryManager.Remove(_memoryManager.GetSizeInBytes(key, item.Value.ToString()));
                    return null;
                }
            }
            else // if StrictExpiry is false, then just return the item if it exists in the cache or return null if it doesn't
            {
                isReadSuccessfully = _cache.TryGetValue(key, out item) ? true : false;
            }
        }
        if (isReadSuccessfully)
        {
            OnReadEvent(item);
        }
        return isReadSuccessfully ? item.Value : null;
    }

    public bool UpdateWholeLock(CacheItem item)
    {
        bool isUpdatedSuccessfully = false;
        string oldValue = "";
        CacheItem oldItem = null;
        long oldSize = 0;
        long newSize = 0;
        lock (_lock)
        {
            if (!_cache.TryGetValue(item.Key, out oldItem))
            {
                log.Error($"Key: {item.Key} does not exist in the cache, cannot update");
                throw new ArgumentException($"Key: {item.Key} does not exist in the cache, cannot update");
            }

            oldValue = oldItem.Value.ToString();
            oldSize = _memoryManager.GetSizeInBytes(item.Key, oldValue);
            newSize = _memoryManager.GetSizeInBytes(item.Key, item.Value.ToString());

            if (!_memoryManager.CanUpdate(oldSize, newSize))
            {
                return false;
            }

            _cache[item.Key] = item;
            isUpdatedSuccessfully = true;
        }

        if (isUpdatedSuccessfully)
        {
            //OnUpdateEvent(item.Key, oldValue, item.Value.ToString());
            OnUpdateEvent(oldItem, item);
            _memoryManager.Update(oldSize, newSize);
            return true;
        }
        else
        {
            log.Error($"Key: {item.Key} does not exist in the cache, cannot update");
            throw new ArgumentException($"Key: {item.Key} does not exist in the cache, cannot update");
        }
        return false;
    }


    /// <summary>
    /// Updates the value of the cache item, if the key does not exist, throws an exception.
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public bool Update(CacheItem item)
    {
        bool isUpdatedSuccessfully = false;
        string oldValue = "";
        CacheItem oldItem = null;
        long oldSize = 0;
        long newSize = 0;

        // lock on the cache to check if the item exists
        lock (_lock)
        {
            if (!_cache.TryGetValue(item.Key, out oldItem))
            {
                log.Error($"Key: {item.Key} does not exist in the cache, cannot update");
                throw new ArgumentException($"Key: {item.Key} does not exist in the cache, cannot update");
            }
        }

        // Lock on the oldItem to perform the update
        // Acquiring the lock on the specific CacheItem is safe, since no other method modifies the existing CacheItem object
        lock (oldItem)
        {
            oldValue = oldItem.Value.ToString();
            oldSize = _memoryManager.GetSizeInBytes(item.Key, oldValue);
            newSize = _memoryManager.GetSizeInBytes(item.Key, item.Value.ToString());

            if (!_memoryManager.CanUpdate(oldSize, newSize))
            {
                return false;
            }
            oldItem.Value = item.Value;
            oldItem.TTL = item.TTL;
            oldItem.IsExpired = item.IsExpired;

            isUpdatedSuccessfully = true;
        }

        if (isUpdatedSuccessfully)
        {
            OnUpdateEvent(oldItem, item);
            _memoryManager.Update(oldSize, newSize);
            return true;
        }
        else
        {
            log.Error($"Key: {item.Key} does not exist in the cache, cannot update");
            throw new ArgumentException($"Key: {item.Key} does not exist in the cache, cannot update");
        }
    }
    public void Delete(string key)
    {
        string removedValue = "";
        CacheItem removedItem = null;
        long size = 0;
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out removedItem) == false)
            {
                log.Warn($"Cannot Remove, key: {key} doesn't exist");
                return;
            }
            else
            {
                removedValue = removedItem.Value.ToString();
                _cache.Remove(key);
            }
        }

        //OnDeleteEvent(key, removedValue);
        OnDeleteEvent(removedItem);
        size = _memoryManager.GetSizeInBytes(key, removedValue);
        _memoryManager.Remove(size);
        return;
    }

    public void Clear()
    {
        try
        {
            lock (_lock)
            {
                _cache.Clear();
            }
            OnFlushAllEvent();
            _memoryManager.Clear();
            return;
        }
        catch (Exception e)
        {
            log.Error($"Error while flushing the cache: {e.Message}");
            throw e;
        }
    }

    protected virtual void OnCreateEvent(CacheItem item)
    {
        CreateEvent?.Invoke(this, new CacheCoreEventArgs(item));
    }

    protected virtual void OnUpdateEvent(CacheItem oldItem, CacheItem newItem)
    {
        UpdateEvent?.Invoke(this, new CacheCoreEventArgs(oldItem, newItem));
    }

    protected virtual void OnDeleteEvent(CacheItem cacheItem)
    {
        DeleteEvent?.Invoke(this, new CacheCoreEventArgs(cacheItem));
    }

    protected virtual void OnReadEvent(CacheItem item)
    {
        ReadEvent?.Invoke(this, new CacheCoreEventArgs(item));
    }

    protected virtual void OnFlushAllEvent()
    {
        FlushAllEvent?.Invoke(this, new EventArgs());
    }

    protected virtual void OnEvictionNeeded()
    {
        EvictionNeeded?.Invoke(this, new EventArgs());
    }


}
