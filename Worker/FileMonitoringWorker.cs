// DAY 5: starts the monitor, drops events on the queue, and runs the processing loop.
using FileMonitoringService.Models;
using FileMonitoringService.Monitoring;
using FileMonitoringService.Services;

namespace FileMonitoringService.Worker;

public sealed class FileMonitoringWorker : BackgroundService
{
    private readonly IDirectoryMonitor _monitor;
    private readonly IFileEventProcessor _processor;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<FileMonitoringWorker> _logger;

    // .NET supplies these four things automatically (dependency injection).
    public FileMonitoringWorker(
        IDirectoryMonitor monitor,
        IFileEventProcessor processor,
        IHostApplicationLifetime lifetime,
        ILogger<FileMonitoringWorker> logger)
    {
        _monitor = monitor;
        _processor = processor;
        _lifetime = lifetime;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("File monitoring service starting at {TimeUtc}", DateTime.UtcNow);

        try
        {
            // SUBSCRIBE to the doorbell, then start watching.
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
            // Run the cook's loop. It keeps going until the service is asked to stop.
            await _processor.ProcessAsync(stoppingToken);
        }
        finally
        {
            // Always runs when stopping (Chapter 12: finally).
            _monitor.EventObserved -= OnEventObserved;    // unsubscribe
            _monitor.Dispose();                           // switch off every watcher
            _logger.LogInformation("File monitoring service stopped at {TimeUtc}", DateTime.UtcNow);
        }
    }

    // The EVENT HANDLER (the waiter): drop the slip on the rail and leave straight away.
    private void OnEventObserved(object? sender, FileSystemEventRecord record)
    {
        if (_processor.TryEnqueue(record))
        {
            _logger.LogInformation("QUEUED {EventType}: {FullPath}", record.EventType, record.FullPath);
        }
        else
        {
            _logger.LogWarning("The event queue is full or closed. Dropped {EventType} event for {FullPath}",
                record.EventType, record.FullPath);
        }
    }
}