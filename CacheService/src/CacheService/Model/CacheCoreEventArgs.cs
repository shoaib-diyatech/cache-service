namespace App.WindowsService;

public class CacheCoreEventArgs : EventArgs
{

    public CacheItem Item { get; }

    public CacheItem? OldItem { get; }

    public CacheCoreEventArgs(CacheItem oldItem, CacheItem newItem)
    {
        OldItem = oldItem;
        Item = newItem;
    }

    public CacheCoreEventArgs(CacheItem item)
    {
        Item = item;
        OldItem = null;
    }
}