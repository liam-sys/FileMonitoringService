// DAY 2, step 2: the settings class.
// The values come from the "FileMonitoring" section of appsettings.json,
// so nothing is typed into the code itself.
namespace FileMonitoringService.Options;

public sealed class FileMonitoringOptions
{
    // The name of the section in appsettings.json. A const never changes (Chapter 14).
    public const string SectionName = "FileMonitoring";

    // The folders to watch. A List holds many values, like a Python list.
    public List<string> Directories { get; set; } = new();

    // true = also watch the folders inside those folders.
    public bool IncludeSubdirectories { get; set; } = true;

    // Which files to watch, for example "*.pdf". "*.*" means every file.
    public string Filter { get; set; } = "*.*";

    // How much room the watcher has to hold events (in KB).
    public int InternalBufferSizeKb { get; set; } = 64;

    // How many events can wait in line before new ones are refused.
    public int ChannelCapacity { get; set; } = 1000;

    // "Changed" events for the same file closer together than this count as one.
    public int DuplicateWindowMs { get; set; } = 500;

    // How many times to try saving one event before giving up.
    public int MaxSaveAttempts { get; set; } = 3;

    // How long to wait before trying again. It doubles after each failure.
    public int RetryBaseDelayMs { get; set; } = 1000;
}