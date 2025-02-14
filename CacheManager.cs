using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Options;

namespace App.WindowsService
{
    public class CacheManager
    {
        //private readonly ConcurrentDictionary<string, byte[]> _cache = new();
        private readonly ConcurrentDictionary<string, string> _cache = new();

        private long _currentMemoryUsageInBytes = 0; // Atomic tracking
        private readonly long _maxMemoryUsageInBytes; // Max memory in bytes

        public CacheManager(IOptions<CacheSettings> cacheSettings)
        {
            _maxMemoryUsageInBytes = cacheSettings.Value.CacheSizeInMBs * 1024 * 1024;// Convert MB to Bytes
        }

        public int getCurrentMemoryUsageInMB()
        {
            return (int)(_currentMemoryUsageInBytes / 1024 / 1024);
        }

        //public bool Create(string key, byte[] serializedValue)
        public bool Create(string key, string serializedValue)
        {
            long size = GetSizeInBytes(key, serializedValue);

            // Check memory limit before adding
            if (Interlocked.Read(ref _currentMemoryUsageInBytes) + size > _maxMemoryUsageInBytes)
            {
                return false; // Reject insertion (Todo: Add Eviction logic here)
            }

            if (_cache.TryAdd(key, serializedValue))
            {
                Interlocked.Add(ref _currentMemoryUsageInBytes, size);
                return true;
            }

            return false;
        }

        public bool Read(string key, out string value) => _cache.TryGetValue(key, out value);

        //public bool Update(string key, byte[] newValue)
        public bool Update(string key, string newValue)
        {
            if (!_cache.TryGetValue(key, out string oldValue)) return false;

            long oldSize = GetSizeInBytes(key, oldValue);
            long newSize = GetSizeInBytes(key, newValue);

            if (Interlocked.Read(ref _currentMemoryUsageInBytes) - oldSize + newSize > _maxMemoryUsageInBytes)
            {
                return false; // Reject update (Eviction logic can be added later)
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
