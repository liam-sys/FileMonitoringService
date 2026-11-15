// DAY 9: automated tests for the business logic in FileEventProcessor.
// Assignment question: "How would you test the event processor without watching a real directory?"
// Answer: give it a FAKE repository and create the events by hand, as done below.
using FileMonitoringService.Data;
using FileMonitoringService.Models;
using FileMonitoringService.Options;
using FileMonitoringService.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FileMonitoringService.Tests;

public class FileEventProcessorTests
{
    // THE FAKE REPOSITORY: a pretend database. It keeps saved events in a list,
    // and it can be told to fail a number of times.
    private sealed class FakeFileEventRepository : IFileEventRepository
    {
        public List<FileSystemEventRecord> Saved { get; } = new();
        public int FailuresBeforeSuccess { get; set; }       // how many calls throw an error before saving works
        public int Calls { get; private set; }               // how many times SaveAsync was called

        public Task SaveAsync(FileSystemEventRecord record, CancellationToken cancellationToken)
        {
            Calls++;

            if (Calls <= FailuresBeforeSuccess)
            {
                throw new InvalidOperationException("Database unavailable");
            }

            Saved.Add(record);
            return Task.CompletedTask;
        }
    }

    // Helper: builds an event record by hand.
    private static FileSystemEventRecord MakeRecord(FileEventType type, string fileName, DateTime? at = null)
    {
        return new FileSystemEventRecord
        {
            EventType = type,
            FileName = fileName,
            FullPath = Path.Combine("watched", fileName),
            DirectoryPath = "watched",
            OccurredAtUtc = at ?? DateTime.UtcNow
        };
    }

    // Helper: builds a processor that uses the fake repository.
    private static FileEventProcessor CreateProcessor(IFileEventRepository repository, int capacity = 100)
    {
        var options = new FileMonitoringOptions
        {
            ChannelCapacity = capacity,
            DuplicateWindowMs = 500,
            MaxSaveAttempts = 3,
            RetryBaseDelayMs = 1                              // tiny delay so the tests stay fast
        };

        return new FileEventProcessor(
            repository,
            new OptionsWrapper<FileMonitoringOptions>(options),
            NullLogger<FileEventProcessor>.Instance);
    }

    // ---------- THE TESTS. Each one follows Arrange, Act, Assert. ----------

    [Fact]
    public async Task Enqueued_event_is_saved()
    {
        // Arrange: a fake database and a processor
        var repository = new FakeFileEventRepository();
        var processor = CreateProcessor(repository);

        // Act: queue one event, then process everything that is waiting
        processor.TryEnqueue(MakeRecord(FileEventType.Created, "a.txt"));
        await processor.DrainAsync(TimeSpan.FromSeconds(5));

        // Assert: exactly one event reached the "database", and it is a Created event
        Assert.Single(repository.Saved);
        Assert.Equal(FileEventType.Created, repository.Saved[0].EventType);
    }

    [Fact]
    public async Task Changed_events_close_together_are_saved_once()
    {
        var repository = new FakeFileEventRepository();
        var processor = CreateProcessor(repository);
        DateTime start = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        processor.TryEnqueue(MakeRecord(FileEventType.Changed, "a.txt", start));
        processor.TryEnqueue(MakeRecord(FileEventType.Changed, "a.txt", start.AddMilliseconds(100)));   // only 100 ms later
        await processor.DrainAsync(TimeSpan.FromSeconds(5));

        Assert.Single(repository.Saved);                      // the second one was a repeat
    }

    [Fact]
    public async Task Changed_events_far_apart_are_both_saved()
    {
        var repository = new FakeFileEventRepository();
        var processor = CreateProcessor(repository);
        DateTime start = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        processor.TryEnqueue(MakeRecord(FileEventType.Changed, "a.txt", start));
        processor.TryEnqueue(MakeRecord(FileEventType.Changed, "a.txt", start.AddMilliseconds(600)));   // 600 ms later: not a repeat
        await processor.DrainAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(2, repository.Saved.Count);
    }

    [Fact]
    public async Task Created_events_are_never_treated_as_duplicates()
    {
        var repository = new FakeFileEventRepository();
        var processor = CreateProcessor(repository);
        DateTime start = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        processor.TryEnqueue(MakeRecord(FileEventType.Created, "a.txt", start));
        processor.TryEnqueue(MakeRecord(FileEventType.Created, "a.txt", start.AddMilliseconds(10)));
        await processor.DrainAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(2, repository.Saved.Count);              // only Changed can be a repeat
    }

    [Fact]
    public async Task Save_is_retried_and_the_error_is_recorded_when_it_finally_works()
    {
        var repository = new FakeFileEventRepository { FailuresBeforeSuccess = 2 };   // fails twice, then works
        var processor = CreateProcessor(repository);

        processor.TryEnqueue(MakeRecord(FileEventType.Deleted, "a.txt"));
        await processor.DrainAsync(TimeSpan.FromSeconds(5));

        Assert.Single(repository.Saved);
        Assert.Equal(3, repository.Calls);                    // 2 failures + 1 success
        Assert.NotNull(repository.Saved[0].ErrorMessage);     // the earlier problem was noted on the row
    }

    [Fact]
    public async Task Service_does_not_crash_when_the_database_always_fails()
    {
        var repository = new FakeFileEventRepository { FailuresBeforeSuccess = int.MaxValue };   // never works
        var processor = CreateProcessor(repository);

        processor.TryEnqueue(MakeRecord(FileEventType.Created, "a.txt"));
        await processor.DrainAsync(TimeSpan.FromSeconds(5));  // must finish WITHOUT throwing an error

        Assert.Empty(repository.Saved);                       // nothing was saved
        Assert.Equal(3, repository.Calls);                    // it gave up after MaxSaveAttempts
    }

    [Fact]
    public void TryEnqueue_returns_false_when_the_queue_is_full()
    {
        var processor = CreateProcessor(new FakeFileEventRepository(), capacity: 1);   // room for only ONE event

        bool first = processor.TryEnqueue(MakeRecord(FileEventType.Created, "a.txt"));
        bool second = processor.TryEnqueue(MakeRecord(FileEventType.Created, "b.txt"));

        Assert.True(first);
        Assert.False(second);                                 // the rail is full
    }

    [Fact]
    public async Task TryEnqueue_returns_false_after_shutdown_has_started()
    {
        var processor = CreateProcessor(new FakeFileEventRepository());

        await processor.DrainAsync(TimeSpan.FromSeconds(1));  // closes the queue

        Assert.False(processor.TryEnqueue(MakeRecord(FileEventType.Created, "a.txt")));
    }
}