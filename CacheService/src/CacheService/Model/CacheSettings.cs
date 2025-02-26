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

}