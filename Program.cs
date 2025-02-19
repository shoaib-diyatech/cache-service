using App.WindowsService;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;
using log4net;
using log4net.Config;
using System.Reflection;
using System.IO;
using System.Collections.Concurrent;
using System.Net.Sockets;

Directory.SetCurrentDirectory(AppContext.BaseDirectory);
// Initialize log4net
var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
XmlConfigurator.Configure(logRepository, new FileInfo("log4net.config"));

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Bind configuration to a strongly-typed class
builder.Services.Configure<CacheSettings>(builder.Configuration.GetSection("CacheSettings"));

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = ".net Cache";
});

// Create a single shared ConcurrentDictionary instance
var sharedCache = new ConcurrentDictionary<string, string>();

//private readonly BlockingCollection<(TcpClient, Response)> _responseQueue = new();
var sharedResponseQueue = new BlockingCollection<(TcpClient, Response)>();

builder.Services.AddSingleton(sharedResponseQueue);

// Register it as a singleton so that the same instance is shared everywhere
builder.Services.AddSingleton(sharedCache);

// Registering EventHandler for DI via container
builder.Services.AddTransient<EventsHandler>();

// Registering CacheManager for DI via container
builder.Services.AddSingleton<CacheManager>();

// Registering MemoryManager for DI via container, being Injected in CacheManager
builder.Services.AddSingleton<MemoryManager>();

// Registering RequestHandler for DI via container
builder.Services.AddTransient<RequestHandler>();

//Registering CacheService for DI via container
builder.Services.AddSingleton<CacheService>();

//Registering the background service 
builder.Services.AddHostedService<WindowsBackgroundService>();

LoggerProviderOptions.RegisterProviderOptions<EventLogSettings, EventLogLoggerProvider>(builder.Services);

IHost host = builder.Build();
host.Run();