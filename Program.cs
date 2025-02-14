using App.WindowsService;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;
using log4net;
using log4net.Config;
using System.Reflection;
using System.IO;

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

// Registering CacheManager for DI via container
builder.Services.AddSingleton<CacheManager>();

// Registering RequestHandler for DI via container
builder.Services.AddTransient<RequestHandler>();

//Registering CacheService for DI via container
builder.Services.AddSingleton<CacheService>();

//Registering the background service 
builder.Services.AddHostedService<WindowsBackgroundService>();

LoggerProviderOptions.RegisterProviderOptions<EventLogSettings, EventLogLoggerProvider>(builder.Services);

IHost host = builder.Build();
host.Run();