// DAY 7: the queue, the processing loop, duplicate handling, and now REAL saving to the database.
//
//   event handler --TryEnqueue--> [ Channel = the rail ] --ProcessAsync--> save to the database
using System.Threading.Channels;
using FileMonitoringService.Data;
using FileMonitoringService.Models;
using FileMonitoringService.Options;
using Microsoft.Extensions.Options;

namespace FileMonitoringService.Services;

public sealed class FileEventProcessor : IFileEventProcessor
{
    private readonly Channel<FileSystemEventRecord> _channel;
    private readonly IFileEventRepository _repository;     // NEW: the thing that saves to the database
    private readonly FileMonitoringOptions _options;
    private readonly ILogger<FileEventProcessor> _logger;

    // Key = file path, value = the time of the last Changed event we ACCEPTED for that file.
    private readonly Dictionary<string, DateTime> _lastChangedUtc = new();

    public FileEventProcessor(
        IFileEventRepository repository,
        IOptions<FileMonitoringOptions> options,
        ILogger<FileEventProcessor> logger)
    {
        _repository = repository;
        _options = options.Value;
        _logger = logger;

        _channel = Channel.CreateBounded<FileSystemEventRecord>(new BoundedChannelOptions(_options.ChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    }

    public bool TryEnqueue(FileSystemEventRecord record)
    {
        return _channel.Writer.TryWrite(record);
    }

    public async Task ProcessAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Event processing started");

        try
        {
            await foreach (FileSystemEventRecord record in _channel.Reader.ReadAllAsync(cancellationToken))
            {
                await HandleAsync(record, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Event processing cancelled because the service is stopping");
        }
    }

    private async Task HandleAsync(FileSystemEventRecord record, CancellationToken cancellationToken)
    {
        if (IsDuplicate(record))
        {
            _logger.LogInformation("IGNORED duplicate {EventType}: {FullPath}", record.EventType, record.FullPath);
            return;
        }

        try
        {
            // NEW: the real save. The 0.3-second pretend delay is gone.
            await _repository.SaveAsync(record, cancellationToken);

            _logger.LogInformation("Stored {EventType} event for {FullPath}", record.EventType, record.FullPath);
        }
        catch (OperationCanceledException)
        {
            throw;      // the service is stopping: let it through
        }
        catch (Exception ex)
        {
            // Chapter 12: try/catch. One failed save must NEVER crash the whole service (FR-03).
            // For now we log it and move on. On Day 8 we add retries.
            _logger.LogError(ex, "Could not store {EventType} event for {FullPath}. The event was NOT saved.",
                record.EventType, record.FullPath);
        }
    }

    private bool IsDuplicate(FileSystemEventRecord record)
    {
        if (record.EventType != FileEventType.Changed)
        {
            return false;
        }

        var window = TimeSpan.FromMilliseconds(_options.DuplicateWindowMs);

        if (_lastChangedUtc.TryGetValue(record.FullPath, out DateTime lastSeen)
            && record.OccurredAtUtc - lastSeen < window)
        {
            return true;
        }

        _lastChangedUtc[record.FullPath] = record.OccurredAtUtc;

        if (_lastChangedUtc.Count > 5000)
        {
            _lastChangedUtc.Clear();
        }

        return false;
    }
}