namespace App.WindowsService;

using System.Collections.Concurrent;
using log4net;

/// <summary>
/// Cache Manager class to manage the cache operations, Uses <see cref="ConcurrentDictionary"/> DataStructure for thread safety.
/// Maintains the current memory usage and max memory usage to avoid memory overflow, via <see cref="MemoryManager"/>.
/// </summary>
public class CacheManager
{
    private readonly CacheManagerCore _cacheManagerCore;
    private static readonly ILog log = LogManager.GetLogger(typeof(CacheManager));

    public event EventHandler<CacheEventArgs> CreateEvent;
    public event EventHandler<CacheEventArgs> UpdateEvent;
    public event EventHandler<CacheEventArgs> DeleteEvent;
    public event EventHandler FlushAllEvent;

    public CacheManager(MemoryManager memoryManager, CacheManagerCore cacheManagerCore)
    {
        _cacheManagerCore = cacheManagerCore;
        _cacheManagerCore.CreateEvent += (sender, args) => HandleCreateEvent(((CacheCoreEventArgs)args).Item);
        _cacheManagerCore.UpdateEvent += (s, e) => UpdateEvent?.Invoke(s, e);
        _cacheManagerCore.DeleteEvent += (s, e) => DeleteEvent?.Invoke(s, e);
        _cacheManagerCore.FlushAllEvent += (s, e) => FlushAllEvent?.Invoke(s, e);
    }

    public double GetCurrentMemoryUsageInMB() => _cacheManagerCore.GetCurrentMemoryUsageInMB();

    public bool Create(string key, string serializedValue)
    {
        return Create(key, serializedValue, 0);
    }

    /// <summary>
    /// Create a new cache entry with key and value, and ttl in seconds.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="serializedValue"></param>
    /// <param name="ttl">if ttl is 0, then cache entry will not expire</param>
    /// <returns></returns>
    public bool Create(string key, string serializedValue, long ttl = 0)
    {
        var item = new CacheItem { Key = key, Value = serializedValue, TTL = ttl };
        return _cacheManagerCore.Create(item);
    }

    /// <summary>
    /// Gets cached object against the given key. Returns null if it does not exist.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public object Read(string key)
    {
        return _cacheManagerCore.Read(key);
    }

    /// <summary>
    /// Updates the complete object in the cache for the given key. Throws exception if key does not exist.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="newValue"></param>
    /// <returns></returns>
    public bool Update(string key, string newValue)
    {
        var item = new CacheItem { Key = key, Value = newValue };
        return _cacheManagerCore.Update(item);
    }

    /// <summary>
    /// Removes the object from cache against the given key. Does nothing if it does not exist.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public void Delete(string key)
    {
        _cacheManagerCore.Delete(key);
    }

    /// <summary>
    /// Clears the cache
    /// </summary>
    public void Clear()
    {
        _cacheManagerCore.Clear();
    }

    protected virtual void HandleCreateEvent(CacheItem item)
    {
        object value = item.Value;
        if(value == null)
        {
            value = string.Empty;
            //log.Error($"Cache item value is null for key: {item.Key}");
            //return;
        }
        CreateEvent?.Invoke(this, new CacheEventArgs(item.Key, value.ToString()));
    }

    protected virtual void OnUpdateEvent(CacheItem oldItem, CacheItem newItem)
    {
        object oldValue = oldItem.Value;
        if(oldValue == null)
        {
            oldValue = string.Empty;
            //log.Error($"Cache item value is null for key: {oldItem.Key}");
            //return;
        }
        object newValue = newItem.Value;
        if(newValue == null)
        {
            newValue = string.Empty;
            //log.Error($"Cache item value is null for key: {newItem.Key}");
            //return;
        }
        UpdateEvent?.Invoke(this, new CacheEventArgs(oldItem.Key, oldValue?.ToString() ?? string.Empty, newValue?.ToString() ?? string.Empty));
    }


    protected virtual void OnDeleteEvent(CacheItem cacheItem)
    {
        object value = cacheItem.Value;
        if (value == null)
        {
            value = string.Empty;
            //log.Error($"Cache item value is null for key: {cacheItem.Key}");
            //return;
        }
        DeleteEvent?.Invoke(this, new CacheEventArgs(cacheItem.Key, value.ToString()));
    }

    protected virtual void OnFlushAllEvent()
    {
        FlushAllEvent?.Invoke(this, new EventArgs());
    }
}
