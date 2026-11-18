// Saves one event to the database.
using FileMonitoringService.Models;
using Microsoft.EntityFrameworkCore;

namespace FileMonitoringService.Data;

public sealed class FileEventRepository : IFileEventRepository
{
    // A "factory" makes a NEW short-lived DbContext each time we need one.
    // A background service runs for days, so it must not keep one open forever.
    private readonly IDbContextFactory<MonitoringDbContext> _contextFactory;

    public FileEventRepository(IDbContextFactory<MonitoringDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task SaveAsync(FileSystemEventRecord record, CancellationToken cancellationToken)
    {
        // "await using" cleans up the context when the method ends 
        await using MonitoringDbContext db = await _contextFactory.CreateDbContextAsync(cancellationToken);

        db.FileSystemEvents.Add(record);                    // add one row
        await db.SaveChangesAsync(cancellationToken);       // send it to SQL Server
    }
}