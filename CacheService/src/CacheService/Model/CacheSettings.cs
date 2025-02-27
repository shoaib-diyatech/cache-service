namespace App.WindowsService;
public class CacheSettings
{
    public int ClientPort { get; set; }
    public int CacheSizeInMBs { get; set; }

    /// <summary>
    /// The factor by which the cache size is reduced when eviction is triggered.
    /// </summary>
    public float EvictionFactor { get; set; }

    /// <summary>
    /// The threshold at which eviction is triggered.
    /// </summary>
    public float EvictionThreshold { get; set; }

    /// <summary>
    /// If true, the cache will strictly adhere to the TTL of the cache items.
    /// If false, items will be removed when read after their TTL.
    /// </summary>
    public bool StrictExpiry { get; set; }

}