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

LoggerProviderOptions.RegisterProviderOptions<
    EventLogSettings, EventLogLoggerProvider>(builder.Services);

// builder.Services.AddSingleton<JokeService>();
builder.Services.AddSingleton<CacheService>();
builder.Services.AddHostedService<WindowsBackgroundService>();

IHost host = builder.Build();
host.Run();