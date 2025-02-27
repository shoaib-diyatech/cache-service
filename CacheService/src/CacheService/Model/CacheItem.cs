namespace App.WindowsService;
public class CacheItem
{
    public string Key { get; set; }
    public object Value { get; set; }
    public long TTL { get; set; }

    public bool IsExpired{ get; set; }

    /// <summary>
    /// Usage count of the cache item.
    /// </summary>
    public int UsageCount { get; set; }
}

//public interface IValue
//{
//    string Value { get; set; }
//}

//public class StringValue : IValue
//{
//    public string Value { get; set; }
//}