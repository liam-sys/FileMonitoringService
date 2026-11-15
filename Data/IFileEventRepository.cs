// DAY 7: the contract for saving events.
// The processor only knows this interface, so in the tests (Day 9) we can give it a fake
// repository instead of a real database.
using FileMonitoringService.Models;

namespace FileMonitoringService.Data;

public interface IFileEventRepository
{
    // Saves one event. Task = it finishes later. CancellationToken = the "please stop" signal.
    Task SaveAsync(FileSystemEventRecord record, CancellationToken cancellationToken);
}