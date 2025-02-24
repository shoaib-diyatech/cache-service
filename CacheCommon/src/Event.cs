namespace CacheCommon;

public class Event : Response
{
    public EventName Name { get; set; }
    public string Message { get; set; }
}