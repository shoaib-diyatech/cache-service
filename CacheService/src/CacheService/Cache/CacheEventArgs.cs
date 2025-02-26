namespace App.WindowsService;

public class CacheEventArgs : EventArgs
    {
        public string Key { get; }
        public string Value { get; }
        public string OldValue { get; }

        public CacheItem Item { get; }
        public CacheEventArgs(CacheItem item)
        {
            Item = item;
            Key = item.Key;
            Value = (string)item.Value;
        }
    
        public CacheEventArgs(string key, string value)
        {
            Key = key;
            Value = value;
        }
    
        public CacheEventArgs(string key, string oldValue, string newValue)
        {
            Key = key;
            OldValue = oldValue;
            Value = newValue;
        }
    }

    public enum CacheEvent{
        Create,
        Update,
        Read,
        Delete,
        FlushAll
    }