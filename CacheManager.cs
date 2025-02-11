namespace App.WindowsService;
using System;
using System.Collections.Concurrent;

public class CacheManager
{
    private readonly ConcurrentDictionary<string, string> _cache = new ConcurrentDictionary<string, string>();

    public bool Create(string key, string value) => _cache.TryAdd(key, value);
    public bool Read(string key, out string value) => _cache.TryGetValue(key, out value);
    public bool Update(string key, string newValue) => _cache.TryUpdate(key, newValue, _cache[key]);
    public bool Delete(string key) => _cache.TryRemove(key, out _);
}
