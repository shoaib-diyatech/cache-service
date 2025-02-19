namespace App.WindowsService;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text.Json;
using System.Text;
using log4net;

public class EventsHandler
{
    private static readonly ILog log = LogManager.GetLogger(typeof(EventsHandler));
    private readonly CacheManager _cacheManager;
    private readonly ConcurrentDictionary<EventName, List<TcpClient>> _eventSubscribers;
    private readonly BlockingCollection<(TcpClient, Response)> _responseQueue;

    public EventsHandler(CacheManager cacheManager, BlockingCollection<(TcpClient, Response)> responseQueue)
    {
        _cacheManager = cacheManager;
        _responseQueue = responseQueue;
        _eventSubscribers = new ConcurrentDictionary<EventName, List<TcpClient>>();

        // Subscribe to CacheManager events
        _cacheManager.CreateEvent += (sender, args) => NotifySubscribers(EventName.Create, args);
        _cacheManager.UpdateEvent += (sender, args) => NotifySubscribers(EventName.Update, args);
        _cacheManager.DeleteEvent += (sender, args) => NotifySubscribers(EventName.Delete, args);
        _cacheManager.FlushAllEvent += (sender, args) => NotifySubscribers(EventName.FlushAll, args);
    }

    public Response Process(TcpClient client, Request request)
    {
        try
        {
            log.Debug($"EventsHandler Request: {JsonSerializer.Serialize(request)}");
            if (request.Command is not SubCommand)
            {
                _responseQueue.Add((client, new Response { RequestId = request.RequestId, Code = Code.BadRequest, Message = "Invalid command" }));
            }
            Response response = request.Command.Execute(request.RequestId, request.Args);
            if (response.Code == Code.Success)
            {
                _responseQueue.Add((client, response));
                log.Debug($"EventsHandler Response: {JsonSerializer.Serialize(response)}");
                if (Enum.TryParse(request.Args[0], true, out EventName eventType))
                {
                    RegisterClient(client, eventType);
                }
            }
            else
            {
                log.Debug($"EventsHandler Response: {JsonSerializer.Serialize(response)}");
                _responseQueue.Add((client, response));
            }
            return null;
        }
        catch (Exception ex)
        {
            return new Response { RequestId = request.RequestId, Code = Code.InternalServerError, Message = ex.Message };
        }
    }

    public void RegisterClient(TcpClient client, EventName eventName)
    {
        log.Debug($"Registering client: {client.Client.RemoteEndPoint} for event: {eventName}");
        if (!_eventSubscribers.ContainsKey(eventName))
        {
            _eventSubscribers[eventName] = new List<TcpClient>();
        }
        _eventSubscribers[eventName].Add(client);
        log.Info($"Client: {client.Client.RemoteEndPoint} subscribed to event: {eventName}");
    }

    public void NotifySubscribers(EventName eventName, CacheEventArgs args)
    {
        log.Debug($"{eventName} fired for key: {args.Key}, value: {args.Value}");
        if (_eventSubscribers.TryGetValue(eventName, out List<TcpClient> subscribers))
        {
            foreach (var client in subscribers)
            {
                log.Debug($"Notifying client: {client.Client.RemoteEndPoint}");
                try
                {
                    var response = new Response
                    {
                        RequestId = Guid.NewGuid().ToString(),
                        Code = Code.Success,
                        Type = Type.Event,
                        Message = $"{eventName} triggered for key: {args.Key}, value: {args.Value}"
                    };
                    _responseQueue.Add((client, response));
                }
                catch (Exception ex)
                {
                    log.Error($"Error notifying client: {client.Client.RemoteEndPoint}, Error: {ex.Message}");
                }
            }
        }
    }
}