using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Options;
using log4net;

namespace App.WindowsService
{
    /// <summary>
    /// Cache Manager class to manage the cache operations, Uses ConcurrentDictionary DataStructure for thread safety.
    /// Maintains the current memory usage and max memory usage to avoid memory overflow.
    /// </summary>
    public class CacheManager
    {
        private readonly ConcurrentDictionary<string, string> _cache = new();

        private static readonly ILog log = LogManager.GetLogger(typeof(CacheService));

        private long _currentMemoryUsageInBytes = 0; // Atomic tracking
        private readonly long _maxMemoryUsageInBytes; // Max memory in bytes

        public CacheManager(IOptions<CacheSettings> cacheSettings)
        {
            _maxMemoryUsageInBytes = cacheSettings.Value.CacheSizeInMBs * 1024 * 1024;// Convert MB to Bytes
            if (!log.IsDebugEnabled)
                log.Info("Debug is not enabled");
            log.Debug("Debug is enabled");
        }

        public double getCurrentMemoryUsageInMB()
        {
            double usageInMB = (double)(_currentMemoryUsageInBytes / 1024.0 / 1024.0);
            return Math.Round(usageInMB, 6); // Rounding to 6 decimal places
        }

        /// <summary>
        /// Flush all the cache items
        /// </summary>
        /// <returns></returns>
        public bool FlushAll()
        {
            try
            {
                _cache.Clear();
                _currentMemoryUsageInBytes = 0;
                return true;
            }
            catch (Exception e)
            {
                log.Error($"Error while flushing the cache: {e.Message}");
                throw e;
            }
        }

        public bool Create(string key, string serializedValue)
        {
            long size = GetSizeInBytes(key, serializedValue);

            // Check memory limit before adding
            if (Interlocked.Read(ref _currentMemoryUsageInBytes) + size > _maxMemoryUsageInBytes)
            {
                log.Error($"Memory limit reached, cannot add more items to the cache, currentMemoryUsageInBytes: {_currentMemoryUsageInBytes}");
                // Todo: Add Eviction logic here
                return false;
            }

            if (_cache.TryAdd(key, serializedValue))
            {
                Interlocked.Add(ref _currentMemoryUsageInBytes, size);
                if (log.IsDebugEnabled)
                    log.Debug($"Added key: {key}, Value: {serializedValue}, currentMemoryUsageInBytes: {_currentMemoryUsageInBytes}");
                return true;
            }
            return false;
        }

        public bool Read(string key, out string value) => _cache.TryGetValue(key, out value);

        public bool Update(string key, string newValue)
        {
            if (!_cache.TryGetValue(key, out string oldValue)) return false;

            long oldSize = GetSizeInBytes(key, oldValue);
            long newSize = GetSizeInBytes(key, newValue);

            if (Interlocked.Read(ref _currentMemoryUsageInBytes) - oldSize + newSize > _maxMemoryUsageInBytes)
            {
                return false; // Todo: Add Eviction logic here
            }

            if (_cache.TryUpdate(key, newValue, oldValue))
            {
                Interlocked.Add(ref _currentMemoryUsageInBytes, newSize - oldSize);
                return true;
            }
            return false;
        }

        public bool Delete(string key)
        {
            if (_cache.TryRemove(key, out string removedValue))
            {
                long size = GetSizeInBytes(key, removedValue);
                Interlocked.Add(ref _currentMemoryUsageInBytes, -size);
                return true;
            }
            return false;
        }

        private long GetSizeInBytes(string key, byte[] value)
        {
            // UTF-16 Encoding (2 bytes per char for .NET strings) + value size
            return (key.Length * 2) + (value.Length * 2);
        }

        private long GetSizeInBytes(string key, string value)
        {
            // UTF-16 Encoding (2 bytes per char for .NET strings) + value size
            return (key.Length * 2) + (value.Length * 2);
        }
    }
}