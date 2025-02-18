namespace App.WindowsService;

using System.Threading;
using log4net;
using Microsoft.Extensions.Options;

/// <summary>
/// Manage the memory usage of the cache to maintain the memory limit.
/// Uses CacheSettings, via DI, to get the max memory limit.
/// </summary>
public class MemoryManager
{
    private static readonly ILog log = LogManager.GetLogger(typeof(MemoryManager));
    private long _currentMemoryUsageInBytes = 0; // Atomic tracking
    private readonly long _maxMemoryUsageInBytes; // Max memory in bytes

    public MemoryManager(IOptions<CacheSettings> cacheSettings)
    {
        _maxMemoryUsageInBytes = cacheSettings.Value.CacheSizeInMBs * 1024 * 1024; // Convert MB to Bytes
    }

    public long CurrentMemoryUsageInBytes => Interlocked.Read(ref _currentMemoryUsageInBytes);

    public bool CanAdd(long size) => Interlocked.Read(ref _currentMemoryUsageInBytes) + size <= _maxMemoryUsageInBytes;

    public bool CanUpdate(long oldSize, long newSize)
    {
        bool canUpdate = Interlocked.Read(ref _currentMemoryUsageInBytes) - oldSize + newSize <= _maxMemoryUsageInBytes;
        return canUpdate;
    }

    public void Add(long size) => Interlocked.Add(ref _currentMemoryUsageInBytes, size);

    public void Update(long oldSize, long newSize) => Interlocked.Add(ref _currentMemoryUsageInBytes, newSize - oldSize);

    public void Remove(long size) => Interlocked.Add(ref _currentMemoryUsageInBytes, -size);

    public double GetCurrentMemoryUsageInMB() => Math.Round((double)(_currentMemoryUsageInBytes / 1024.0 / 1024.0), 6); // Rounding to 6 decimal places

    public long GetSizeInBytes(string key, string value)
    {
        return GetSizeInBytes(key) + GetSizeInBytes(value);
    }

    /// <summary>
    /// Get the size of the string in bytes based on the UTF-16 encoding.
    /// </summary>
    /// <param name="value">String value</param>
    /// <returns>Size of the string in bytes</returns>
    public long GetSizeInBytes(string value){
        // UTF-16 Encoding (2 bytes per char for .NET strings)
        // ConcurrentDictionary uses UTF-16 encoding for strings
        return value.Length * 2;
    }
}