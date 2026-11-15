// DAY 7: Entity Framework Core mapping between our C# class and the SQL table (FR-03).
using FileMonitoringService.Models;
using Microsoft.EntityFrameworkCore;

namespace FileMonitoringService.Data;

public class MonitoringDbContext : DbContext
{
    public MonitoringDbContext(DbContextOptions<MonitoringDbContext> options) : base(options)
    {
    }

    // One DbSet = one table. This one is the FileSystemEvents table.
    public DbSet<FileSystemEventRecord> FileSystemEvents => Set<FileSystemEventRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FileSystemEventRecord>(entity =>
        {
            entity.ToTable("FileSystemEvents");
            entity.HasKey(e => e.Id);

            // Store the enum as text (Created, Changed...) so the table is easy to read.
            entity.Property(e => e.EventType).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(e => e.FileName).HasMaxLength(260).IsRequired();
            entity.Property(e => e.FullPath).HasMaxLength(1024).IsRequired();
            entity.Property(e => e.OldFullPath).HasMaxLength(1024);
            entity.Property(e => e.DirectoryPath).HasMaxLength(1024).IsRequired();
            entity.Property(e => e.OccurredAtUtc).HasColumnType("datetime2").IsRequired();
            entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
        });
    }
}