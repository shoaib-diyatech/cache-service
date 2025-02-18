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

public sealed class CacheService
{
    private static readonly ILog log = LogManager.GetLogger(typeof(CacheService));
    private readonly CacheSettings _settings;
    private readonly ILogger<CacheService> _logger;
    private static TcpListener? _listener;
    private int Port;
    private int? MaxConnections;
    private SemaphoreSlim _connectionLimiter;
    private readonly RequestHandler _requestHandler;
    /// <summary>
    /// Delimiter for separating requests, Windows uses \r\n for new lines, for Unix it is \n
    /// </summary>
    private const string Delimiter = "\r\n";

    private const int bufferSize = 4;
    private readonly BlockingCollection<(TcpClient, Request)> _requestQueue = new();
    private readonly BlockingCollection<(TcpClient, Response)> _responseQueue = new();

    public CacheService(ILogger<CacheService> logger, IOptions<CacheSettings> settings, RequestHandler requestHandler)
    {
        _logger = logger;
        _settings = settings.Value;
        _logger.LogInformation("CacheService initialized");
        log.Info("CacheService constructor called");
        _requestHandler = requestHandler;
        // Todo: add a connection limiter
        // _connectionLimiter = _settings.MaxConnections;
    }

    public async Task Start(CancellationToken stoppingToken)
    {
        Port = _settings.Port;
        _listener = new TcpListener(IPAddress.Any, Port);
        _listener.Start();
        //Console.WriteLine($"Server started on port: {Port}");
        _logger.LogInformation($"Server listening on port {Port}");
        log.Info($"Cache Service listening on port {Port}");

        // Start a new task asynchronously to process requests
        Task.Run(() => ProcessRequests(stoppingToken), stoppingToken);
        // Start a new task asynchronously to send responses
        Task.Run(() => SendResponses(stoppingToken), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            // Wait for a client to connect
            TcpClient client = await _listener.AcceptTcpClientAsync();
            // Handle a new client
            _ = HandleClientAsync(client);
        }
        _listener.Stop(); // Stop listening when service shuts down
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
                    //log.Info($"Received: {requestString}");
                    if (log.IsDebugEnabled)
                        log.Debug($"Client: {client.Client.RemoteEndPoint} Received: {requestString}");
                    try
                    {
                        Request request = Request.Parse(requestString, _requestHandler.CommandFactory);
                        // Adding a dynamic tuple (TcpClient, Request) to the request queue
                        _requestQueue.Add((client, request));
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
            Task.Run(() => ProcessRequest(client, request));
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
        Response response = _requestHandler.ProcessRequest(request);
        _responseQueue.Add((client, response));
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