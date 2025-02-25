namespace App.WindowsService;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text.Json;
using System.Text;
using log4net;
using CacheCommon;
using Microsoft.Extensions.Options;

public sealed class ReqEventHandler : MessageHandler
{
    private readonly CommandFactory _commandFactory = new CommandFactory();
    private readonly CommandExecutor _commandExecutor;
    private readonly IOptions<CacheSettings> _cacheSettings;
    private readonly CacheManager _serviceProvider;
    private readonly ConcurrentDictionary<EventName, List<TcpClient>> _eventSubscribers;

    private static readonly ILog log = LogManager.GetLogger(typeof(ReqEventHandler));

    public ReqEventHandler(IOptions<CacheSettings> cacheSettings, CacheManager serviceProvider, CommandExecutor commandExecutor, BlockingCollection<(TcpClient, Response)> responseQueue)
        : base(responseQueue)
    {
        _cacheSettings = cacheSettings;
        _serviceProvider = serviceProvider;
        _commandExecutor = commandExecutor;
        _eventSubscribers = new ConcurrentDictionary<EventName, List<TcpClient>>();

        // Subscribe to CacheManager events
        _serviceProvider.CreateEvent += (sender, args) => NotifySubscribers(EventName.Create, args);
        _serviceProvider.UpdateEvent += (sender, args) => NotifySubscribers(EventName.Update, args);
        _serviceProvider.DeleteEvent += (sender, args) => NotifySubscribers(EventName.Delete, args);
        _serviceProvider.FlushAllEvent += (sender, args) => NotifySubscribers(EventName.FlushAll, args);
    }

    public CommandFactory CommandFactory => _commandFactory;

    public override void Process(TcpClient client, Request request)
    {
        try
        {
            if (request.Type != RequestType.Event)
            {
                _responseQueue.Add((client, new Response { RequestId = request.RequestId, Code = Code.BadRequest, Message = "Invalid request type" }));
                return;
            }

            try
            {
                Response response = _commandExecutor.Execute(request.RequestId, request.Command);
                _responseQueue.Add((client, response));
                if (request.Command is SubCommand)
                {
                    SubCommand subCommand = (SubCommand)request.Command;
                    RegisterClient(client, subCommand.EventType);
                }
                else if (request.Command is UnsubCommand)
                {
                    UnsubCommand unsubCommand = (UnsubCommand)request.Command;
                    UnregisterClient(client, unsubCommand.EventType);
                }
            }
            catch (Exception ex)
            {
                _responseQueue.Add((client, new Response { RequestId = request.RequestId, Code = Code.InternalServerError, Message = ex.Message }));
            }
        }
        catch (Exception ex)
        {
            _responseQueue.Add((client, new Response { RequestId = request.RequestId, Code = Code.InternalServerError, Message = ex.Message }));
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

    public void UnregisterClient(TcpClient client, EventName eventName)
    {
        log.Debug($"Unregistering client: {client.Client.RemoteEndPoint} for event: {eventName}");
        if (_eventSubscribers.ContainsKey(eventName))
        {
            _eventSubscribers[eventName].Remove(client);
            log.Info($"Client: {client.Client.RemoteEndPoint} unsubscribed from event: {eventName}");
        }
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
                        Type = ResponseType.Event,
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
