namespace App.WindowsService;

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using log4net.Config;
using Microsoft.Extensions.Options;
using CacheCommon;

public sealed class CacheService
{
    private static readonly ILog log = LogManager.GetLogger(typeof(CacheService));
    private readonly CacheSettings _settings;
    private readonly ILogger<CacheService> _logger;
    private static TcpListener? _clientListener;
    private int ClientPort;
    private int? MaxConnections;
    private SemaphoreSlim _connectionLimiter;
    //private readonly RequestHandler _requestHandler;
    //private readonly ReqEventHandler _eventHandler;

    private readonly MessageHandler _messageRequestHandler;
    private readonly MessageHandler _messageEventsHandler;

    /// <summary>
    /// Delimiter for separating requests, Windows uses \r\n for new lines, for Unix it is \n
    /// </summary>
    private const string Delimiter = "\r\n";

    private const int bufferSize = 32;
    private readonly BlockingCollection<(TcpClient, Request)> _requestQueue = new();

    private readonly BlockingCollection<(TcpClient, Response)> _responseQueue;

    public CacheService(ILogger<CacheService> logger, IOptions<CacheSettings> settings, RequestHandler requestHandler, ReqEventHandler eventHandler, BlockingCollection<(TcpClient, Response)> responseQueue)
    {
        _logger = logger;
        _settings = settings.Value;
        _logger.LogInformation("CacheService initialized");
        log.Info("CacheService constructor called");
        _responseQueue = responseQueue;
        _messageRequestHandler = requestHandler;
        _messageEventsHandler = eventHandler;

        // Todo: add a connection limiter
        // _connectionLimiter = _settings.MaxConnections;
    }

    public async Task Start(CancellationToken stoppingToken)
    {
        ClientPort = _settings.ClientPort;
        _clientListener = new TcpListener(IPAddress.Any, ClientPort);
        _clientListener.Start();
        _logger.LogInformation($"Server listening on port {ClientPort}");
        log.Info($"Cache Service listening on port {ClientPort}");

        // Start a new task asynchronously to process requests
        Task.Run(() => ProcessRequests(stoppingToken), stoppingToken);

        // Start a new task asynchronously to send responses
        Task.Run(() => SendResponses(stoppingToken), stoppingToken);

        // Start a new task for listener
        Task.Run(() => StartListener(stoppingToken), stoppingToken);
    }

    public async Task Stop()
    {
        _clientListener?.Stop();
        _logger.LogInformation("Server stopped");
        log.Info("Cache Service stopped");
    }


    private async Task StartListener(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Wait for a client to connect
            TcpClient client = await _clientListener.AcceptTcpClientAsync();
            // Handle a new client
            _ = HandleClientAsync(client);
        }
        _clientListener.Stop(); // Stop listening when service shuts down
    }



    /// <summary>
    /// Handle a new client connection, adds the requests to the request queue
    /// </summary>
    /// <param name="client"></param>
    /// <returns></returns>
    private async Task HandleClientAsync(TcpClient client)
    {
        Console.WriteLine($"Client connected: {client.Client.RemoteEndPoint}");
        log.Info($"Client connected: {client.Client.RemoteEndPoint}");
        NetworkStream stream = client.GetStream();
        byte[] buffer = new byte[bufferSize];
        StringBuilder requestBuilder = new StringBuilder();

        try
        {
            while (true)
            {
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0) break;

                string chunk = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                requestBuilder.Append(chunk);

                string accumulatedData = requestBuilder.ToString();
                int delimiterIndex;// = accumulatedData.IndexOf(Delimiter);
                // Loop to handle multiple requests in a single buffer
                while ((delimiterIndex = accumulatedData.IndexOf(Delimiter)) >= 0)
                {
                    // Drop the delimeter string from the accumulated data
                    string requestString = accumulatedData.Substring(0, delimiterIndex);
                    // Extract the remaining data after the delimiter, could contain a part of the next request, or even a complete request
                    accumulatedData = accumulatedData.Substring(delimiterIndex + Delimiter.Length);
                    // Clearing the requestBuilder since we have extracted the request upto the delimiter
                    requestBuilder.Clear();
                    // Appending the remaining data to the requestBuilder, if any, otherwise we will loose the data after the delimiter upto the end of the buffer
                    requestBuilder.Append(accumulatedData);

                    Console.WriteLine($"Received: {requestString}");
                    Request request = null;
                    try
                    {
                        request = JsonSerializer.Deserialize<Request>(requestString);
                        if (request != null)
                            _requestQueue.Add((client, request));

                        {
                            _requestQueue.Add((client, request));
                        }
                    }
                    catch (JsonException ex)
                    {
                        if (request == null)
                        {
                            try
                            {
                                request = Request.Parse(requestString);
                            }
                            catch (Exception exInner)
                            {
                                Console.WriteLine($"Error parsing request: {exInner.Message}");
                                log.Error($"Error parsing request: {exInner.Message}");
                            }
                        }
                        if (request != null)
                            _requestQueue.Add((client, request));
                        else
                        {
                            Console.WriteLine($"Error parsing request: {ex.Message}");
                            log.Error($"Error parsing request: {ex.Message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error parsing request: {ex.Message}");
                        log.Error($"Error parsing request: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Client: {client.Client.RemoteEndPoint}, Error: {ex.Message}");
            log.Error($"Client: {client.Client.RemoteEndPoint}, Error: {ex.Message}");
        }
        finally
        {
            client.Close();
            Console.WriteLine($"Client disconnected.{client.Client.RemoteEndPoint}");
            log.Info($"Client disconnected: {client.Client.RemoteEndPoint}");
        }
    }

    /// <summary>
    /// Processes requests from the request blocking queue, on receiving a new request, it processes the request in a new task
    /// </summary>
    /// <param name="stoppingToken"></param>
    private void ProcessRequests(CancellationToken stoppingToken)
    {
        // BlockingQueue waits for new requests to be added to the request queue
        foreach (var (client, request) in _requestQueue.GetConsumingEnumerable(stoppingToken))
        {
            //Run the ProcessRequest method in a new task
            //TOdo: remove the sub task from here
            //Task.Run(() => ProcessRequest(client, request));
            // Not running ProcessRequest in a new task, to avoid frequent context switching
            ProcessRequest(client, request);
        }
    }


    /// <summary>
    /// Processes a request, invokes the RequestHandler to process the request and adds the response to the response queue
    /// Blocks to receive the response from the RequestHandler
    /// </summary>
    /// <param name="client"></param>
    /// <param name="request"></param>
    private void ProcessRequest(TcpClient client, Request request)
    {
        if (request.Type == RequestType.Event)
        {
            _messageEventsHandler.Process(client, request);
        }
        else
        {
            _messageRequestHandler.Process(client, request);
        }
        //if (request.Command is SubCommand)
        //{
        //    _eventHandler.Process(client, request); // No need to add the response to the response queue, eventHandler adds the response to the response queue
        //    // // Handle REGISTER command by forwarding to EventHandler
        //    // _eventHandler.RegisterClient(client, request.Args[0]);
        //    // _responseQueue.Add((client, new Response
        //    // {
        //    //     RequestId = request.RequestId,
        //    //     Code = Code.Success,
        //    //     Type = Type.Response,
        //    //     Message = "Registered for event successfully."
        //    // }));
        //}
        //else
        //{
        //    // Todo: do not recevie a response here, Requesthandler should add the response to the response queue
        //    Response response = _requestHandler.ProcessRequest(request);
        //    _responseQueue.Add((client, response));
        //}
    }

    /// <summary>
    /// Sends responses to the clients, blocks to receive responses from the response queue
    /// </summary>
    /// <param name="stoppingToken"></param>
    private void SendResponses(CancellationToken stoppingToken)
    {
        foreach (var (client, response) in _responseQueue.GetConsumingEnumerable(stoppingToken))
        {
            try
            {
                NetworkStream stream = client.GetStream();
                string responseJson = JsonSerializer.Serialize(response) + Delimiter;
                byte[] responseBytes = Encoding.UTF8.GetBytes(responseJson);
                stream.WriteAsync(responseBytes, 0, responseBytes.Length).Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending response: {ex.Message}, to client: {client.Client.RemoteEndPoint}");
                log.Error($"Error sending response: {ex.Message}, to client: {client.Client.RemoteEndPoint}");
            }
        }
    }
}