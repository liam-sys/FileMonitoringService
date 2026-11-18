// Checks the settings when the service starts (requirement FR-05).
// If something is wrong, the service refuses to start and lists ALL the problems at once.
using Microsoft.Extensions.Options;

namespace FileMonitoringService.Options;

public sealed class FileMonitoringOptionsValidator : IValidateOptions<FileMonitoringOptions>
{
    public ValidateOptionsResult Validate(string? name, FileMonitoringOptions options)
    {
        // This list collects every problem we find.
        var failures = new List<string>();

        // There must be at least one folder to watch.
        if (options.Directories.Count == 0)
        {
            failures.Add("FileMonitoring:Directories must contain at least one directory path.");
        }

        // Check each folder path.
        for (int i = 0; i < options.Directories.Count; i++)
        {
            string directory = options.Directories[i];

            if (string.IsNullOrWhiteSpace(directory))
            {
                failures.Add($"FileMonitoring:Directories[{i}] is empty.");
            }
            else if (!Path.IsPathRooted(directory))
            {
                // A full path starts with a drive such as C:\ . "Reports" alone is not enough.
                failures.Add($"FileMonitoring:Directories[{i}] must be a full path, but was '{directory}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(options.Filter))
        {
            failures.Add("FileMonitoring:Filter must not be empty (use *.* for all files).");
        }

        if (options.InternalBufferSizeKb < 4 || options.InternalBufferSizeKb > 1024)
        {
            failures.Add("FileMonitoring:InternalBufferSizeKb must be between 4 and 1024.");
        }

        if (options.ChannelCapacity < 1)
        {
            failures.Add("FileMonitoring:ChannelCapacity must be at least 1.");
        }

        if (options.DuplicateWindowMs < 0)
        {
            failures.Add("FileMonitoring:DuplicateWindowMs must not be negative.");
        }

        if (options.MaxSaveAttempts < 1)
        {
            failures.Add("FileMonitoring:MaxSaveAttempts must be at least 1.");
        }

        if (options.RetryBaseDelayMs < 0)
        {
            failures.Add("FileMonitoring:RetryBaseDelayMs must not be negative.");
        }

        // We do NOT check here that the folders exist on the disk.
        // The monitoring code does that, so one missing folder can be skipped
        // while the other folders keep being watched.

        if (failures.Count > 0)
        {
            return ValidateOptionsResult.Fail(failures);
        }

        return ValidateOptionsResult.Success;
    }
}