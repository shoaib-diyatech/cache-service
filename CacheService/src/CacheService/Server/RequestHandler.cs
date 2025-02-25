namespace App.WindowsService;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using CacheCommon;
using System.Net.Sockets;
using System.Collections.Concurrent;
using log4net;
using System.Text.Json;

/// <summary>
/// Processes the <see cref="Request"/> and adds the <see cref="Response"/> in the response queue.
/// </summary>
public sealed class RequestHandler : MessageHandler
{
    private readonly CommandFactory _commandFactory = new CommandFactory();
    private readonly CommandExecutor _commandExecutor;
    private readonly IOptions<CacheSettings> _cacheSettings;
    private readonly CacheManager _serviceProvider;

    public RequestHandler(IOptions<CacheSettings> cacheSettings, CacheManager serviceProvider, CommandExecutor commandExecutor, BlockingCollection<(TcpClient, Response)> responseQueue)
        : base(responseQueue)
    {
        _cacheSettings = cacheSettings;
        _serviceProvider = serviceProvider;
        _commandExecutor = commandExecutor;
    }

    public CommandFactory CommandFactory => _commandFactory;

    public override void Process(TcpClient client, Request request)
    {
        try
        {
            Response response = _commandExecutor.Execute(request.RequestId, request.Command);
            _responseQueue.Add((client, response));
        }
        catch (Exception ex)
        {
            _responseQueue.Add((client, new Response { RequestId = request.RequestId, Code = Code.InternalServerError, Message = ex.Message }));
        }
    }

}
