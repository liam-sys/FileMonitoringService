// DAY 8: the queue, duplicate handling, saving with RETRIES, and a safe shutdown (DRAIN).
//
//   event handler --TryEnqueue--> [ Channel = the rail ] --ProcessAsync--> save (retry if it fails)
using System.Threading.Channels;
using FileMonitoringService.Data;
using FileMonitoringService.Models;
using FileMonitoringService.Options;
using Microsoft.Extensions.Options;

namespace FileMonitoringService.Services;

public sealed class FileEventProcessor : IFileEventProcessor
{
    private readonly Channel<FileSystemEventRecord> _channel;
    private readonly IFileEventRepository _repository;
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

    // NEW (Day 8): the safe shutdown. "Closing time at the restaurant".
    public async Task DrainAsync(TimeSpan timeout)
    {
        // 1. Stop taking new orders. After this, TryEnqueue returns false.
        _channel.Writer.TryComplete();

        _logger.LogInformation("Saving the events still waiting in the queue (time limit: {Timeout})", timeout);

        // 2. A stopwatch: after the time limit, this token says "stop now".
        using var cts = new CancellationTokenSource(timeout);

        try
        {
            // 3. The cook finishes every slip still on the rail.
            await foreach (FileSystemEventRecord record in _channel.Reader.ReadAllAsync(cts.Token))
            {
                await HandleAsync(record, cts.Token);
            }

            _logger.LogInformation("The queue is empty. Nothing was left behind.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("The shutdown time limit was reached. Events still in the queue were not saved.");
        }
    }

    private async Task HandleAsync(FileSystemEventRecord record, CancellationToken cancellationToken)
    {
        if (IsDuplicate(record))
        {
            _logger.LogInformation("IGNORED duplicate {EventType}: {FullPath}", record.EventType, record.FullPath);
            return;
        }

        // NEW (Day 8): THE RECOVERY POLICY (FR-03).
        // Try up to MaxSaveAttempts times, waiting longer after each failure (1s, 2s, 4s...).
        // If every attempt fails, log the error with the event details and carry on
        // with the next event. The service never crashes because of one failed save.
        string? lastError = null;

        for (int attempt = 1; attempt <= _options.MaxSaveAttempts; attempt++)
        {
            try
            {
                // If an earlier attempt failed, note it on the row we are about to save.
                if (lastError != null)
                {
                    record.ErrorMessage = Truncate("Saved after a retry. Previous error: " + lastError, 1000);
                }

                await _repository.SaveAsync(record, cancellationToken);

                _logger.LogInformation("Stored {EventType} event for {FullPath}", record.EventType, record.FullPath);
                return;                                       // success: we are done with this event
            }
            catch (OperationCanceledException)
            {
                throw;                                        // the service is stopping: let it through
            }
            catch (Exception ex)
            {
                lastError = ex.Message;

                // Was that the last attempt? Then give up on this event.
                if (attempt >= _options.MaxSaveAttempts)
                {
                    _logger.LogError(ex,
                        "Could not store {EventType} event for {FullPath} after {Attempts} attempts. The event was NOT saved.",
                        record.EventType, record.FullPath, attempt);
                    return;
                }

                // Otherwise wait, then try again. The wait doubles each time: 1s, 2s, 4s...
                TimeSpan delay = TimeSpan.FromMilliseconds(_options.RetryBaseDelayMs * Math.Pow(2, attempt - 1));

                _logger.LogWarning(ex,
                    "Database save failed (attempt {Attempt} of {MaxAttempts}). Retrying in {Delay}.",
                    attempt, _options.MaxSaveAttempts, delay);

                await Task.Delay(delay, cancellationToken);
            }
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

    // Cuts a long text down to a maximum length (the database column holds 1000 characters).
    private static string Truncate(string text, int maxLength)
    {
        return text.Length <= maxLength ? text : text.Substring(0, maxLength);
    }
}