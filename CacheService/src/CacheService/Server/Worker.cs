namespace App.WindowsService;
using log4net;
using log4net.Config;

public sealed class WindowsBackgroundService(
    CacheService cacheService,
    ILogger<WindowsBackgroundService> logger) : BackgroundService
{
    private static readonly ILog log = LogManager.GetLogger(typeof(WindowsBackgroundService));
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                logger.LogInformation("Cache Service is starting...");
                log.Info("Cache Service is starting...");
                await cacheService.Start(stoppingToken); // Start listening on the TCP port

                //await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                // Wait until the cancellation token is triggered
                await Task.Delay(Timeout.Infinite, stoppingToken);

            }
        }
        catch (OperationCanceledException)
        {
            // When the stopping token is canceled, for example, a call made from services.msc,
            // we shouldn't exit with a non-zero exit code. In other words, this is expected...
            logger.LogWarning("Service is stopping...");
            log.Debug("Service is stopping...");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected Error: {Message}", ex.Message);
            log.Error($"Unexpected Error: {ex.Message}", ex);

            // Terminates this process and returns an exit code to the operating system.
            // This is required to avoid the 'BackgroundServiceExceptionBehavior', which
            // performs one of two scenarios:
            // 1. When set to "Ignore": will do nothing at all, errors cause zombie services.
            // 2. When set to "StopHost": will cleanly stop the host, and log errors.
            //
            // In order for the Windows Service Management system to leverage configured
            // recovery options, we need to terminate the process with a non-zero exit code.
            Environment.Exit(1);
        }
    }
}