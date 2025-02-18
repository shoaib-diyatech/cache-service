namespace App.WindowsService;

using System.Threading;
using log4net;

public class MemoryManager
{
    private static readonly ILog log = LogManager.GetLogger(typeof(MemoryManager));
    private long _currentMemoryUsageInBytes = 0; // Atomic tracking
    private readonly long _maxMemoryUsageInBytes; // Max memory in bytes

    public MemoryManager(long maxMemoryUsageInBytes)
    {
        _maxMemoryUsageInBytes = maxMemoryUsageInBytes;
    }

    public long CurrentMemoryUsageInBytes => Interlocked.Read(ref _currentMemoryUsageInBytes);

    public bool CanAdd(long size) => Interlocked.Read(ref _currentMemoryUsageInBytes) + size <= _maxMemoryUsageInBytes;

    public bool CanUpdate(long oldSize, long newSize) => Interlocked.Read(ref _currentMemoryUsageInBytes) - oldSize + newSize <= _maxMemoryUsageInBytes;

    public void Add(long size) => Interlocked.Add(ref _currentMemoryUsageInBytes, size);

    public void Update(long oldSize, long newSize) => Interlocked.Add(ref _currentMemoryUsageInBytes, newSize - oldSize);

    public void Remove(long size) => Interlocked.Add(ref _currentMemoryUsageInBytes, -size);

    public double GetCurrentMemoryUsageInMB() => Math.Round((double)(_currentMemoryUsageInBytes / 1024.0 / 1024.0), 6); // Rounding to 6 decimal places

    public long GetSizeInBytes(string key, string value)
    {
        // UTF-16 Encoding (2 bytes per char for .NET strings) + value size
        return (key.Length * 2) + (value.Length * 2);
    }
}