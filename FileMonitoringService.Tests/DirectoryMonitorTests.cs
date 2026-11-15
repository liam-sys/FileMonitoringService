// DAY 9: tests for DirectoryMonitor. These use a real temporary folder, so they are
// "integration" tests. They cover the missing-folder policy and a real Created event.
using FileMonitoringService.Models;
using FileMonitoringService.Monitoring;
using FileMonitoringService.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FileMonitoringService.Tests;

public class DirectoryMonitorTests
{
    // Helper: builds a monitor with the given settings.
    private static DirectoryMonitor CreateMonitor(FileMonitoringOptions options)
    {
        return new DirectoryMonitor(
            new OptionsWrapper<FileMonitoringOptions>(options),
            NullLogger<DirectoryMonitor>.Instance);
    }

    [Fact]
    public void Start_throws_when_no_configured_directory_exists()
    {
        // Arrange: a folder name that does not exist
        string missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var options = new FileMonitoringOptions { Directories = { missing } };

        using DirectoryMonitor monitor = CreateMonitor(options);

        // Act + Assert: with no valid folder, Start must stop with an error
        Assert.Throws<InvalidOperationException>(() => monitor.Start());
    }

    [Fact]
    public void Start_continues_when_at_least_one_directory_exists()
    {
        DirectoryInfo temp = Directory.CreateTempSubdirectory("fms-test-");

        try
        {
            string missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var options = new FileMonitoringOptions { Directories = { missing, temp.FullName } };

            using DirectoryMonitor monitor = CreateMonitor(options);

            monitor.Start();     // must NOT throw: one valid folder is enough
        }
        finally
        {
            temp.Delete(true);   // always tidy up the temporary folder
        }
    }

    [Fact]
    public async Task Raises_a_Created_event_when_a_file_is_created()
    {
        DirectoryInfo temp = Directory.CreateTempSubdirectory("fms-test-");

        try
        {
            var options = new FileMonitoringOptions { Directories = { temp.FullName } };

            using DirectoryMonitor monitor = CreateMonitor(options);

            // A TaskCompletionSource is a "wait for it" object: the test waits until the event arrives.
            var received = new TaskCompletionSource<FileSystemEventRecord>(TaskCreationOptions.RunContinuationsAsynchronously);

            monitor.EventObserved += (sender, record) =>
            {
                if (record.EventType == FileEventType.Created)
                {
                    received.TrySetResult(record);
                }
            };

            monitor.Start();

            // Act: create a real file
            await File.WriteAllTextAsync(Path.Combine(temp.FullName, "hello.txt"), "test");

            // Wait up to 5 seconds for the event to arrive.
            Task finished = await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(5)));

            // Assert: the event arrived (if not, we timed out) and it has the right file name
            Assert.Same(received.Task, finished);
            Assert.Equal("hello.txt", received.Task.Result.FileName);
        }
        finally
        {
            temp.Delete(true);
        }
    }
}