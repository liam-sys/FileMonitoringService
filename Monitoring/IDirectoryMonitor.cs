// The "contract" for a directory monitor.
// An interface lists WHAT a class can do, not HOW it does it.
// Other classes will depend on this list, not on the real monitor,
// so tests can swap in a fake monitor later.
using FileMonitoringService.Models;

namespace FileMonitoringService.Monitoring;

public interface IDirectoryMonitor : IDisposable
{
    // The DOORBELL. Other classes can subscribe to be told when a file event is seen.
    //   delegate      = the rule for what an answering method looks like (here: EventHandler<T>)
    //   event         = the doorbell itself; outsiders can only subscribe, only the monitor can ring it
    //   event handler = the method that answers when the bell rings
    event EventHandler<FileSystemEventRecord>? EventObserved;

    // Starts watching every valid folder from the settings.
    void Start();
}