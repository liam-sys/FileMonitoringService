// DAY 5, step 1: the contract for the event processor.
using FileMonitoringService.Models;

namespace FileMonitoringService.Services;

public interface IFileEventProcessor
{
    // Called by the event handler. It only drops the event into the queue, so it is instant.
    // Returns true when the event was accepted, and false when the queue is full or closed.
    bool TryEnqueue(FileSystemEventRecord record);

    // Runs until the service is asked to stop. It takes events out of the queue
    // one by one and handles them.
    // Task = an operation that finishes later. CancellationToken = the "please stop" signal.
    Task ProcessAsync(CancellationToken cancellationToken);
}