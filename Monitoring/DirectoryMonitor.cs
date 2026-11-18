// The monitor that owns the FileSystemWatcher objects (requirements FR-01 and FR-02).
// It watches the folders and turns each file event into a FileSystemEventRecord,
// then rings the EventObserved doorbell.
using FileMonitoringService.Models;
using FileMonitoringService.Options;
using Microsoft.Extensions.Options;

namespace FileMonitoringService.Monitoring;

public sealed class DirectoryMonitor : IDirectoryMonitor
{
    private readonly FileMonitoringOptions _options;
    private readonly ILogger<DirectoryMonitor> _logger;
    private readonly List<FileSystemWatcher> _watchers = new();
    private bool _disposed;

    // The constructor .NET hands us the settings and a logger automatically.
    // This is called "dependency injection": we never write "new" for them ourselves.
    public DirectoryMonitor(IOptions<FileMonitoringOptions> options, ILogger<DirectoryMonitor> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    // The doorbell that was promised in the interface.
    public event EventHandler<FileSystemEventRecord>? EventObserved;

    public void Start()
    {
        foreach (string directory in _options.Directories)
        {
            // POLICY for a missing folder: log a clear error and SKIP it.
            // The other valid folders keep being monitored.
            if (!Directory.Exists(directory))
            {
                _logger.LogError("Configured directory {Directory} does not exist or is not accessible. It will be skipped.", directory);
                continue;
            }

            try
            {
                CreateWatcher(directory);
                _logger.LogInformation("Monitoring {Directory} (include subdirectories: {IncludeSubdirectories})",
                    directory, _options.IncludeSubdirectories);
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
            {
                // try/catch. One bad folder must not stop the others.
                _logger.LogError(ex, "Could not start watching {Directory}. It will be skipped.", directory);
            }
        }

        // If NOTHING is being watched, there is no point running: stop with a clear message.
        if (_watchers.Count == 0)
        {
            throw new InvalidOperationException(
                "None of the configured directories could be monitored. Check FileMonitoring:Directories in appsettings.json.");
        }
    }

    private void CreateWatcher(string directory)
    {
        var watcher = new FileSystemWatcher(directory)
        {
            IncludeSubdirectories = _options.IncludeSubdirectories,
            Filter = _options.Filter,
            InternalBufferSize = _options.InternalBufferSizeKb * 1024,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
        };

        // The four callbacks. Each one runs when the watcher sees that kind of event.
        // They must be FAST, so they only create a record and ring our doorbell.
        watcher.Created += (sender, e) => Raise(FileEventType.Created, e.FullPath, null, directory);
        watcher.Deleted += (sender, e) => Raise(FileEventType.Deleted, e.FullPath, null, directory);
        watcher.Renamed += (sender, e) => Raise(FileEventType.Renamed, e.FullPath, e.OldFullPath, directory);
        watcher.Changed += (sender, e) =>
        {
            // A folder's "last write" time changes when files inside it change.
            // We only care about files, so ignore folders.
            if (Directory.Exists(e.FullPath))
            {
                return;
            }

            Raise(FileEventType.Changed, e.FullPath, null, directory);
        };

        // The Error event tells us when the watcher itself has a problem (for example its buffer is full).
        watcher.Error += (sender, e) => OnWatcherError(directory, e.GetException());

        watcher.EnableRaisingEvents = true;       // start watching
        _watchers.Add(watcher);
    }

    // Builds the record and rings the doorbell.
    private void Raise(FileEventType eventType, string fullPath, string? oldFullPath, string monitoredDirectory)
    {
        var record = new FileSystemEventRecord
        {
            EventType = eventType,
            FullPath = fullPath,
            OldFullPath = oldFullPath,
            FileName = Path.GetFileName(fullPath),
            DirectoryPath = monitoredDirectory,
            OccurredAtUtc = DateTime.UtcNow            // UTC = the same time everywhere
        };

        try
        {
            EventObserved?.Invoke(this, record);       // ring the bell (the ?. skips it when nobody listens)
        }
        catch (Exception ex)
        {
            // An error escaping from a watcher callback could crash the whole program.
            _logger.LogError(ex, "A handler failed while processing {EventType} for {FullPath}", eventType, fullPath);
        }
    }

    private void OnWatcherError(string directory, Exception exception)
    {
        if (exception is InternalBufferOverflowException)
        {
            // Too many events arrived at once and some were LOST. This is a known limit of FileSystemWatcher.
            _logger.LogError(exception,
                "Watcher buffer overflow in {Directory}. Some file events were lost. Consider increasing FileMonitoring:InternalBufferSizeKb.",
                directory);
        }
        else
        {
            _logger.LogError(exception, "Watcher error in {Directory}. The watcher for this folder may have stopped.", directory);
        }
    }

    // Cleans up when the service stops: switch off and release every watcher.
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (FileSystemWatcher watcher in _watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }

        _watchers.Clear();
        _logger.LogInformation("All directory watchers were disposed");
    }
}