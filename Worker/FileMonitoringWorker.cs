// DAY 4: starts the monitor and listens for file events.
// (This is a simple version. On Day 5 and 6 we add the queue and the database.)
using FileMonitoringService.Models;
using FileMonitoringService.Monitoring;

namespace FileMonitoringService.Worker;

// BackgroundService gives us ExecuteAsync. .NET calls it when the service starts
// and cancels stoppingToken when the service is asked to stop (Ctrl + C, or closing the window).
public sealed class FileMonitoringWorker : BackgroundService
{
    private readonly IDirectoryMonitor _monitor;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<FileMonitoringWorker> _logger;

    // .NET supplies these three things automatically (dependency injection).
    public FileMonitoringWorker(
        IDirectoryMonitor monitor,
        IHostApplicationLifetime lifetime,
        ILogger<FileMonitoringWorker> logger)
    {
        _monitor = monitor;
        _lifetime = lifetime;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("File monitoring service starting at {TimeUtc}", DateTime.UtcNow);

        try
        {
            // SUBSCRIBE to the doorbell: "ring OnEventObserved when a file event happens".
            _monitor.EventObserved += OnEventObserved;
            _monitor.Start();
        }
        catch (InvalidOperationException ex)
        {
            // No valid folder at all: stop the service with a clear message.
            _logger.LogCritical(ex, "Monitoring could not start, so the service will stop.");
            _lifetime.StopApplication();
            return;
        }

        try
        {
            // Wait here until the service is asked to stop.
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // This is normal: it is how we find out that we were asked to stop.
        }
        finally
        {
            // Always runs when stopping (Chapter 12: finally).
            _monitor.EventObserved -= OnEventObserved;    // unsubscribe
            _monitor.Dispose();                           // switch off every watcher
            _logger.LogInformation("File monitoring service stopped at {TimeUtc}", DateTime.UtcNow);
        }
    }

    // The EVENT HANDLER: the method that answers the doorbell.
    // It runs every time the monitor raises EventObserved.
    private void OnEventObserved(object? sender, FileSystemEventRecord record)
    {
        _logger.LogInformation("EVENT {EventType}: {FullPath} | old path: {OldFullPath} | {OccurredAtUtc:O}",
            record.EventType, record.FullPath, record.OldFullPath ?? "-", record.OccurredAtUtc);
    }
}