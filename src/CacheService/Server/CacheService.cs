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
        byte[] buffer = new byte[1024];
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
                int delimiterIndex;
                while ((delimiterIndex = accumulatedData.IndexOf(Delimiter)) >= 0)
                {
                    string requestString = accumulatedData.Substring(0, delimiterIndex);
                    accumulatedData = accumulatedData.Substring(delimiterIndex + Delimiter.Length);
                    requestBuilder.Clear();
                    requestBuilder.Append(accumulatedData);

                    Console.WriteLine($"Received: {requestString}");
                    log.Info($"Received: {requestString}");
                    if (log.IsDebugEnabled)
                        log.Debug($"Client: {client.Client.RemoteEndPoint} Received: {requestString}");

                    Request request = Request.Parse(requestString, _requestHandler.CommandFactory);
                    // Adding a dynamic tuple (TcpClient, Request) to the request queue
                    _requestQueue.Add((client, request));
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            log.Error($"Error: {ex.Message}");
        }
        finally
        {
            client.Close();
            Console.WriteLine("Client disconnected.");
            log.Info($"Client disconnected: {client.Client.RemoteEndPoint}");
        }
    }

    private void ProcessRequests(CancellationToken stoppingToken)
    {
        // BlockingQueue waits for new requests to be added to the request queue
        foreach (var (client, request) in _requestQueue.GetConsumingEnumerable(stoppingToken))
        {
            //Run the ProcessRequest method in a new task
            Task.Run(() => ProcessRequest(client, request));
        }
    }

    private void ProcessRequest(TcpClient client, Request request)
    {
        Response response = _requestHandler.ProcessRequest(request);
        _responseQueue.Add((client, response));
    }


    private void SendResponses(CancellationToken stoppingToken)
    {
        foreach (var (client, response) in _responseQueue.GetConsumingEnumerable(stoppingToken))
        {
            try
            {
                NetworkStream stream = client.GetStream();
                string responseJson = JsonSerializer.Serialize(response);
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