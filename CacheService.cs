namespace App.WindowsService;

using System;
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

    ILogger<CacheService> _logger;
    private static TcpListener? _listener;
    private int Port = 9090;
    private int? MaxConnections = 10;
    private SemaphoreSlim _connectionLimiter;

    private static readonly RequestHandler _requestHandler = new RequestHandler();

    /**
    * Create a TcpServer instance , defaults to 10 number of connections if null is provided
    */
    // public CacheService(int port, int? maxConnections)
    // {
    //     Port = port;
    //     MaxConnections = maxConnections ?? 10;
    //     _connectionLimiter = new(MaxConnections.Value);
    // }

    public CacheService(ILogger<CacheService> logger, IOptions<CacheSettings> settings)
    {
        _logger = logger;
        _settings = settings.Value;
        _logger.LogInformation("CacheService constructor called");
        log.Info("CacheService constructor called");
        _connectionLimiter = new(MaxConnections.Value);
    }

    public async Task Start(CancellationToken stoppingToken)
    {
        Port = _settings.Port;
        _listener = new TcpListener(IPAddress.Any, Port);
        _listener.Start();
        Console.WriteLine($"Server started on port {Port}");
        _logger.LogInformation($"Server started on port {Port}");
        log.Info($"Server started on port {Port}");
        //Program.log.Info($"Server started on port {Port}");

        while (!stoppingToken.IsCancellationRequested)
        {
            TcpClient client = await _listener.AcceptTcpClientAsync();
            _ = HandleClientAsync(client); // Handle clients asynchronously
        }
        _listener.Stop(); // Stop listening when service shuts down
    }

    private static async Task HandleClientAsync(TcpClient client)
    {
        Console.WriteLine($"Client connected: {client.Client.RemoteEndPoint}");
        log.Info($"Client connected: {client.Client.RemoteEndPoint}");
        NetworkStream stream = client.GetStream();
        byte[] buffer = new byte[1024];

        try
        {
            while (true)
            {
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0) break;

                string request = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                Console.WriteLine($"Received: {request}");
                log.Info($"Received: {request}");

                string response = _requestHandler.ProcessRequest(request);
                byte[] responseBytes = Encoding.UTF8.GetBytes(response);
                await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
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
            log.Info("Client disconnected.");
        }
    }
}