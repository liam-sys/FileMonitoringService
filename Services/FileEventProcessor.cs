// DAY 5, step 2: the queue and the loop that handles events one by one.
//
//   event handler --TryEnqueue--> [ Channel = the rail ] --ProcessAsync--> handle each event
//
// The handler only drops the event on the rail (instant). A separate loop does the slow work,
// so the watcher never has to wait.
using System.Threading.Channels;
using FileMonitoringService.Models;
using FileMonitoringService.Options;
using Microsoft.Extensions.Options;

namespace FileMonitoringService.Services;

public sealed class FileEventProcessor : IFileEventProcessor
{
    private readonly Channel<FileSystemEventRecord> _channel;
    private readonly ILogger<FileEventProcessor> _logger;

    public FileEventProcessor(IOptions<FileMonitoringOptions> options, ILogger<FileEventProcessor> logger)
    {
        _logger = logger;

        // A BOUNDED channel has a maximum size (ChannelCapacity from appsettings.json),
        // so the queue can never grow forever and eat all the memory.
        _channel = Channel.CreateBounded<FileSystemEventRecord>(new BoundedChannelOptions(options.Value.ChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,   // when full, TryWrite says "no" instead of blocking
            SingleReader = true,                      // only ProcessAsync takes events out
            SingleWriter = false                      // many watcher threads can put events in
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
            // "await foreach" waits for the next event without freezing the program.
            await foreach (FileSystemEventRecord record in _channel.Reader.ReadAllAsync(cancellationToken))
            {
                await HandleAsync(record, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal: this is how we learn that the service is stopping (Ctrl + C).
            _logger.LogInformation("Event processing cancelled because the service is stopping");
        }
    }

    private async Task HandleAsync(FileSystemEventRecord record, CancellationToken cancellationToken)
    {
        // TEMPORARY (Day 5 only): pretend the work is slow, like a database save will be.
        // We remove this delay on Day 7.
        await Task.Delay(300, cancellationToken);

        _logger.LogInformation("PROCESSED {EventType}: {FullPath}", record.EventType, record.FullPath);
    }
}