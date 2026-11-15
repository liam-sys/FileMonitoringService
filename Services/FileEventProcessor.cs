// DAY 6: the queue, the processing loop, and duplicate "Changed" handling.
//
//   event handler --TryEnqueue--> [ Channel = the rail ] --ProcessAsync--> handle each event
//
// NEW today: Windows often raises 2 or 3 Changed events for ONE save.
// We keep the first and ignore the repeats that arrive within DuplicateWindowMs.
using System.Threading.Channels;
using FileMonitoringService.Models;
using FileMonitoringService.Options;
using Microsoft.Extensions.Options;

namespace FileMonitoringService.Services;

public sealed class FileEventProcessor : IFileEventProcessor
{
    private readonly Channel<FileSystemEventRecord> _channel;
    private readonly FileMonitoringOptions _options;
    private readonly ILogger<FileEventProcessor> _logger;

    // NEW: a Dictionary (like a Python dict). Key = file path, value = the time of the
    // last Changed event we ACCEPTED for that file.
    private readonly Dictionary<string, DateTime> _lastChangedUtc = new();

    public FileEventProcessor(IOptions<FileMonitoringOptions> options, ILogger<FileEventProcessor> logger)
    {
        _options = options.Value;
        _logger = logger;

        // A BOUNDED channel has a maximum size (ChannelCapacity from appsettings.json).
        _channel = Channel.CreateBounded<FileSystemEventRecord>(new BoundedChannelOptions(_options.ChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    }

    // Put the event on the rail. Returns false when the rail is full or closed.
    public bool TryEnqueue(FileSystemEventRecord record)
    {
        return _channel.Writer.TryWrite(record);
    }

    // The cook: takes events off the rail one at a time until we are asked to stop.
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
        // NEW: check for a repeat BEFORE doing any work.
        if (IsDuplicate(record))
        {
            _logger.LogInformation("IGNORED duplicate {EventType}: {FullPath}", record.EventType, record.FullPath);
            return;
        }

        // TEMPORARY (until Day 7): pretend the work is slow, like a database save will be.
        await Task.Delay(300, cancellationToken);

        _logger.LogInformation("PROCESSED {EventType}: {FullPath}", record.EventType, record.FullPath);
    }

    // NEW: is this a repeat of a Changed event we just accepted for the same file?
    private bool IsDuplicate(FileSystemEventRecord record)
    {
        // Created, Deleted and Renamed are never repeats.
        if (record.EventType != FileEventType.Changed)
        {
            return false;
        }

        var window = TimeSpan.FromMilliseconds(_options.DuplicateWindowMs);

        // Ask the dictionary: "when did we last accept a Changed event for this file?"
        // TryGetValue gives true and the time if the file is in the dictionary.
        if (_lastChangedUtc.TryGetValue(record.FullPath, out DateTime lastSeen)
            && record.OccurredAtUtc - lastSeen < window)
        {
            return true;        // too soon after the last one: it is a repeat
        }

        // Not a repeat: remember this time for next time.
        _lastChangedUtc[record.FullPath] = record.OccurredAtUtc;

        // Keep memory small on a service that runs for days.
        if (_lastChangedUtc.Count > 5000)
        {
            _lastChangedUtc.Clear();
        }

        return false;
    }
}