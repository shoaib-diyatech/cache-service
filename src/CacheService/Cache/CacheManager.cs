namespace App.WindowsService;

using System.Collections.Concurrent;
using log4net;

/// <summary>
/// Cache Manager class to manage the cache operations, Uses <see cref="ConcurrentDictionary"/> DataStructure for thread safety.
/// Maintains the current memory usage and max memory usage to avoid memory overflow, via <see cref="MemoryManager"/>.
/// </summary>
public class CacheManager
{
    //private readonly ConcurrentDictionary<string, string> _cache;
    private readonly Dictionary<string, string> _cache;

    private readonly object _lock = new object();
    private static readonly ILog log = LogManager.GetLogger(typeof(CacheManager));
    private readonly MemoryManager _memoryManager;
    //private readonly EventsHandler _eventHandler;
    //private readonly ExpiryManager _expiryManager;

    public event EventHandler<CacheEventArgs> CreateEvent;
    public event EventHandler<CacheEventArgs> UpdateEvent;
    public event EventHandler<CacheEventArgs> DeleteEvent;
    public event EventHandler<CacheEventArgs> FlushAllEvent;

    public event EventHandler<CacheEventArgs> EvictionNeeded;

    public CacheManager(Dictionary<string, string> cache, MemoryManager memoryManager)
    {
        _cache = cache;
        _memoryManager = memoryManager;
        //_eventHandler = eventHandler;

        // // Register event handlers
        // CreateEvent += (sender, args) => _eventHandler.NotifySubscribers("CreateEvent", args);
        // UpdateEvent += (sender, args) => _eventHandler.NotifySubscribers("UpdateEvent", args);
        // DeleteEvent += (sender, args) => _eventHandler.NotifySubscribers("DeleteEvent", args);
        // FlushAllEvent += (sender, args) => _eventHandler.NotifySubscribers("FlushAllEvent", args);
        // EvictionNeeded += (sender, args) => _eventHandler.NotifySubscribers("EvictionNeeded", args);

        // CreateEvent += (sender, args) => { };
        // UpdateEvent += (sender, args) => { };
        // DeleteEvent += (sender, args) => { };
        // FlushAllEvent += (sender, args) => { };
        // EvictionNeeded += (sender, args) => { };
        // _expiryManager = expiryManager;
    }

    public double GetCurrentMemoryUsageInMB() => _memoryManager.GetCurrentMemoryUsageInMB();

    public bool Create(string key, string serializedValue)
    {
        //Todo: Add ExpiryManager logic for ttl and call Create(string key, string serializedValue)
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
        long size = _memoryManager.GetSizeInBytes(key, serializedValue);

        // Check memory limit before adding
        if (!_memoryManager.CanAdd(size))
        {
            log.Error($"Memory limit reached, cannot add more items to the cache, currentMemoryUsageInBytes: {_memoryManager.CurrentMemoryUsageInBytes}");
            OnEvictionNeeded();
            return false;
        }
        // to track if cache entry was done successfully, minimizing lock retention time
        bool isInsertedSuccessfully = false;
        lock (_lock)
        {
            //if (_cache.TryAdd(key, serializedValue)) // old code for ConcurrentDictionary
            if (_cache.ContainsKey(key) == false)
            {
                _cache.Add(key, serializedValue);
                isInsertedSuccessfully = true;
            }
            else
            {
                log.Error($"Key: {key} already exists in the cache, cannot add duplicate key");
                // Requirement: throw exception if key already exists
                throw new ArgumentException($"Key: {key} already exists in the cache, cannot add duplicate key");
            }
        }
        if (isInsertedSuccessfully)
        {
            _memoryManager.Add(size);
            // Fire Event after adding in cache
            OnCreateEvent(key, serializedValue);
            // _expiryManager.Add(key);
            log.Debug($"Added key: {key}, Value: {serializedValue}, currentMemoryUsageInBytes: {_memoryManager.CurrentMemoryUsageInBytes}");
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets cached object against the given key. Returns null if it does not exist.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public object Read(string key)
    {
        lock (_lock)
        {
            // Not using direct access _cache[key] to avoid
            // KeyNotFound Exception if key does not exist
            return _cache.TryGetValue(key, out string value) ? value : null;
        }
    }

    /// <summary>
    /// Updates the complete object in the cache for the given key. Throws exception if key does not exist.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="newValue"></param>
    /// <returns></returns>
    public bool Update(string key, string newValue)
    {
        bool isUpdatedSuccessfully = false;
        //if (!_cache.TryGetValue(key, out string oldValue)) return false;
        //bool keyExists = false;
        //string oldValue = (string)Read(key);
        //if (oldValue == null)
        //{
        //    throw new ArgumentException($"Key: {key} does not exist in the cache, cannot update");
        //}
        //else { keyExists = true; }
        string oldValue = "";
        long oldSize = 0;
        long newSize = 0;
        lock (_lock)
        {
            // Cannot call Read method here, since it will cause a deadlock
            // Old value is required to calculate the size of the old value
            if (!_cache.TryGetValue(key, out oldValue))
            {
                // Loging only temporarily, will be removed after testing
                log.Error($"Key: {key} does not exist in the cache, cannot update");
                // Requirement: throw exception if key does not exist
                throw new ArgumentException($"Key: {key} does not exist in the cache, cannot update");
            }

            oldSize = _memoryManager.GetSizeInBytes(key, oldValue);
            newSize = _memoryManager.GetSizeInBytes(key, newValue);

            if (!_memoryManager.CanUpdate(oldSize, newSize))
            {
                return false;
            }

            //if (_cache.TryUpdate(key, newValue, oldValue)) // old code for ConcurrentDictionary
            //if (_cache.ContainsKey(key)) // No need to check here, since we already know that key exists
            //if(keyExists) // If this line is reached, then keyExists will always be true
            {
                _cache[key] = newValue;
                isUpdatedSuccessfully = true;
            }
        }
        if (isUpdatedSuccessfully)
        {
            OnUpdateEvent(key, oldValue, newValue);
            _memoryManager.Update(oldSize, newSize);
            return true;
        }
        else
        {
            log.Error($"Key: {key} does not exist in the cache, cannot update");
            // Requirement: throw exception if key does not exist
            throw new ArgumentException($"Key: {key} does not exist in the cache, cannot update");
        }
        return false;
    }

    /// <summary>
    /// Removes the object from cache against the given key. Does nothing if it does not exist.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public void Delete(string key)
    {
        string removedValue = "";
        long size = 0;
        //if (_cache.TryRemove(key, out string removedValue)) // old code for ConcurrentDictionary
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out removedValue) == false)
            {
                // Do nothing since key doesn't exist, need to remove loging later
                log.Warn($"Cannot Remove, key: {key} doesn't exist");
                return;
            }
            else
            {
                _cache.Remove(key);
            }
        }

        OnDeleteEvent(key, removedValue);
        size = _memoryManager.GetSizeInBytes(key, removedValue);
        _memoryManager.Remove(size);
        //_expiryManager.Remove(key);
        return;
    }

    /// <summary>
    /// Clears the cache
    /// </summary>
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
            //return true;
            return;
        }

        catch (Exception e)
        {
            log.Error($"Error while flushing the cache: {e.Message}");
            throw e;
        }
    }

    // public void Register(
    //     EventHandler<CacheEventArgs> createHandler = null,
    //     EventHandler<CacheEventArgs> updateHandler = null,
    //     EventHandler<CacheEventArgs> deleteHandler = null,
    //     EventHandler<CacheEventArgs> flushAllHandler = null,
    //     EventHandler<CacheEventArgs> evictionNeededHandler = null)
    // {
    //     if (createHandler != null) CreateEvent += createHandler;
    //     if (updateHandler != null) UpdateEvent += updateHandler;
    //     if (deleteHandler != null) DeleteEvent += deleteHandler;
    //     if (flushAllHandler != null) FlushAllEvent += flushAllHandler;
    //     if (evictionNeededHandler != null) EvictionNeeded += evictionNeededHandler;
    // }

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
}