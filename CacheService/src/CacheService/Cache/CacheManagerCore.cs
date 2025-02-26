namespace App.WindowsService;

using System.Collections.Concurrent;
using log4net;

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

    public event EventHandler<CacheEventArgs> CreateEvent;
    public event EventHandler<CacheEventArgs> UpdateEvent;
    public event EventHandler<CacheEventArgs> DeleteEvent;
    public event EventHandler<CacheEventArgs> FlushAllEvent;
    public event EventHandler<CacheEventArgs> EvictionNeeded;

    public event EventHandler<CacheEventArgs> ReadEvent;

    public CacheManagerCore(MemoryManager memoryManager)
    {
        _cache = new();
        _memoryManager = memoryManager;
    }

    public double GetCurrentMemoryUsageInMB() => _memoryManager.GetCurrentMemoryUsageInMB();

    public bool Create(CacheItem item)
    {
        long size = _memoryManager.GetSizeInBytes(item.Key, item.Value.ToString());

        if (!_memoryManager.CanAdd(size))
        {
            log.Error($"Memory limit reached, cannot add more items to the cache, currentMemoryUsageInBytes: {_memoryManager.CurrentMemoryUsageInBytes}");
            OnEvictionNeeded();
            return false;
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
            OnCreateEvent(item.Key, item.Value.ToString());
            log.Debug($"Added key: {item.Key}, Value: {item.Value}, currentMemoryUsageInBytes: {_memoryManager.CurrentMemoryUsageInBytes}");
            return true;
        }
        return false;
    }

    public object Read(string key)
    {
        bool isReadSuccessfully = false;
        CacheItem item = null;
        lock (_lock)
        {
            isReadSuccessfully = _cache.TryGetValue(key, out item) ? true : false;
        }
        if(isReadSuccessfully)
        {
            OnReadEvent(key);
        }
        return isReadSuccessfully ? item.Value : null;
    }

    public bool Update(CacheItem item)
    {
        bool isUpdatedSuccessfully = false;
        string oldValue = "";
        long oldSize = 0;
        long newSize = 0;
        lock (_lock)
        {
            if (!_cache.TryGetValue(item.Key, out CacheItem oldItem))
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
            OnUpdateEvent(item.Key, oldValue, item.Value.ToString());
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

    public void Delete(string key)
    {
        string removedValue = "";
        long size = 0;
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out CacheItem item) == false)
            {
                log.Warn($"Cannot Remove, key: {key} doesn't exist");
                return;
            }
            else
            {
                removedValue = item.Value.ToString();
                _cache.Remove(key);
            }
        }

        OnDeleteEvent(key, removedValue);
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

    protected virtual void OnCreateEvent(string key, string value)
    {
        CreateEvent?.Invoke(this, new CacheEventArgs(key, value));
    }

    protected virtual void OnUpdateEvent(string key, string oldValue, string newValue)
    {
        UpdateEvent?.Invoke(this, new CacheEventArgs(key, oldValue, newValue));
    }

    protected virtual void OnDeleteEvent(string key, string value)
    {
        DeleteEvent?.Invoke(this, new CacheEventArgs(key, value));
    }

    protected virtual void OnFlushAllEvent()
    {
        FlushAllEvent?.Invoke(this, new CacheEventArgs(string.Empty, string.Empty));
    }

    protected virtual void OnEvictionNeeded()
    {
        EvictionNeeded?.Invoke(this, new CacheEventArgs(string.Empty, string.Empty));
    }

    protected virtual void OnReadEvent(string key)
    {
        ReadEvent?.Invoke(this, new CacheEventArgs(key, string.Empty));
    }
}
