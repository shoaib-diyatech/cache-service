namespace App.WindowsService;

using System.Collections.Concurrent;
using System.Linq;

public class EvictionManager
{
    private readonly ConcurrentDictionary<string, int> _usageCounts = new();
    private readonly CacheManager _cacheManager;

    public EvictionManager(CacheManager cacheManager)
    {
        _cacheManager = cacheManager;
        _cacheManager.EvictionNeeded += OnEvictionNeeded;
    }

    public void IncrementUsage(string key)
    {
        _usageCounts.AddOrUpdate(key, 1, (k, v) => v + 1);
    }

    private void OnEvictionNeeded(object sender, EventArgs e)
    {
        Evict();
    }

    public void Evict()
    {
        var leastUsedKey = _usageCounts.OrderBy(kvp => kvp.Value).FirstOrDefault().Key;
        if (leastUsedKey != null)
        {
            _cacheManager.Delete(leastUsedKey);
        }
    }
}