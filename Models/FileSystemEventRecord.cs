// DAY 3, step 1: the information we keep about one file event (requirements FR-02 and FR-03).
// One object of this class = one row in the database table later.
namespace FileMonitoringService.Models;

// An enum is a fixed list of named values (Chapter 14).
// An event can only be one of these four types.
public enum FileEventType
{
    Created,
    Changed,
    Deleted,
    Renamed
}

public class FileSystemEventRecord
{
    public long Id { get; set; }                                    // the database fills this in later

    public FileEventType EventType { get; set; }                    // Created, Changed, Deleted or Renamed

    public string FileName { get; set; } = string.Empty;            // just the name, for example report.pdf

    public string FullPath { get; set; } = string.Empty;            // the whole path to the file

    public string? OldFullPath { get; set; }                        // only used for Renamed (the ? means it can be empty)

    public string DirectoryPath { get; set; } = string.Empty;       // which monitored folder it happened in

    public DateTime OccurredAtUtc { get; set; }                     // when it happened, in UTC time

    public string? ErrorMessage { get; set; }                       // used later if saving needs a retry
}