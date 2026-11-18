// The contract for the event processor (now with a safe shutdown step).
using FileMonitoringService.Models;

namespace FileMonitoringService.Services;

public interface IFileEventProcessor
{
    // Called by the event handler. It only drops the event into the queue, so it is instant.
    // Returns true when the event was accepted, and false when the queue is full or closed.
    bool TryEnqueue(FileSystemEventRecord record);

    // Runs until the service is asked to stop. It takes events out of the queue
    // one by one and handles them.
    Task ProcessAsync(CancellationToken cancellationToken);

    // called when the service is stopping.
    // It stops accepting new events and saves the events still waiting, for at most the given time.
    Task DrainAsync(TimeSpan timeout);
}