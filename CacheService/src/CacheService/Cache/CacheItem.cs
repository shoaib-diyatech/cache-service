public class CacheItem
{
    public string Key { get; set; }
    public IValue Value { get; set; }
    public long TTL { get; set; }
}

public interface IValue
{
    string Value { get; set; }
}

public class StringValue : IValue
{
    public string Value { get; set; }
}