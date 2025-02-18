namespace App.WindowsService;

using System.Collections.Concurrent;
using log4net;

/// <summary>
/// Cache Manager class to manage the cache operations, Uses <see cref="ConcurrentDictionary"/> DataStructure for thread safety.
/// Maintains the current memory usage and max memory usage to avoid memory overflow, via <see cref="MemoryManager"/>.
/// </summary>
public class CacheManager
{
    private readonly ConcurrentDictionary<string, string> _cache;
    private static readonly ILog log = LogManager.GetLogger(typeof(CacheManager));
    private readonly MemoryManager _memoryManager;
    //private readonly ExpiryManager _expiryManager;

    public event EventHandler<CacheEventArgs> CreateEvent;
    public event EventHandler<CacheEventArgs> UpdateEvent;
    public event EventHandler<CacheEventArgs> DeleteEvent;
    public event EventHandler FlushAllEvent;

    public event EventHandler EvictionNeeded;

    public CacheManager(MemoryManager memoryManager)
    {
        _cache = new ConcurrentDictionary<string, string>();
        _memoryManager = memoryManager;
        // _expiryManager = expiryManager;
    }

    public double GetCurrentMemoryUsageInMB() => _memoryManager.GetCurrentMemoryUsageInMB();

    public bool Create(string key, string serializedValue, long ttl)
    {
        //Todo: Add ExpiryManager logic for ttl and call Create(string key, string serializedValue)
        return Create(key, serializedValue);
    }
    public bool Create(string key, string serializedValue)
    {
        long size = _memoryManager.GetSizeInBytes(key, serializedValue);

        // Check memory limit before adding
        if (!_memoryManager.CanAdd(size))
        {
            log.Error($"Memory limit reached, cannot add more items to the cache, currentMemoryUsageInBytes: {_memoryManager.CurrentMemoryUsageInBytes}");
            OnEvictionNeeded();
            return false;
        }

        if (_cache.TryAdd(key, serializedValue))
        {
            OnCreateEvent(key, serializedValue);
            _memoryManager.Add(size);

            // _expiryManager.Add(key);
            log.Debug($"Added key: {key}, Value: {serializedValue}, currentMemoryUsageInBytes: {_memoryManager.CurrentMemoryUsageInBytes}");
            return true;
        }
        return false;
    }

    public bool Read(string key, out string value) => _cache.TryGetValue(key, out value);

    public bool Update(string key, string newValue)
    {
        if (!_cache.TryGetValue(key, out string oldValue)) return false;

        long oldSize = _memoryManager.GetSizeInBytes(key, oldValue);
        long newSize = _memoryManager.GetSizeInBytes(key, newValue);

        if (!_memoryManager.CanUpdate(oldSize, newSize))
        {
            return false;
        }

        if (_cache.TryUpdate(key, newValue, oldValue))
        {
            OnUpdateEvent(key, oldValue, newValue);
            _memoryManager.Update(oldSize, newSize);
            return true;
        }
        return false;
    }

    public bool Delete(string key)
    {
        if (_cache.TryRemove(key, out string removedValue))
        {
            OnDeleteEvent(key, removedValue);
            long size = _memoryManager.GetSizeInBytes(key, removedValue);
            _memoryManager.Remove(size);
            //_expiryManager.Remove(key);
            return true;
        }
        return false;
    }

    public bool FlushAll()
    {
        try
        {
            _cache.Clear();
            OnFlushAllEvent();
            _memoryManager.Remove(_memoryManager.CurrentMemoryUsageInBytes);
            return true;
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
        FlushAllEvent?.Invoke(this, EventArgs.Empty);
    }
    
    protected virtual void OnEvictionNeeded()
    {
        EvictionNeeded?.Invoke(this, EventArgs.Empty);
    }

}