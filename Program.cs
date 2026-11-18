using FileMonitoringService.Data;
using FileMonitoringService.Monitoring;
using FileMonitoringService.Options;
using FileMonitoringService.Services;
using FileMonitoringService.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

// Read the FileMonitoring settings and check them when the service starts
builder.Services.AddSingleton<IValidateOptions<FileMonitoringOptions>, FileMonitoringOptionsValidator>();
builder.Services
    .AddOptions<FileMonitoringOptions>()
    .Bind(builder.Configuration.GetSection(FileMonitoringOptions.SectionName))
    .ValidateOnStart();

// The database. Read the connection string from appsettings.json.
string? connectionString = builder.Configuration.GetConnectionString("MonitoringDb");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:MonitoringDb is missing. Add it to appsettings.json.");
}

// A "factory" that creates a short-lived database connection each time we save an event.
builder.Services.AddDbContextFactory<MonitoringDbContext>(dbOptions => dbOptions.UseSqlServer(connectionString));

// "When someone asks for the interface, give them the real class."
builder.Services.AddSingleton<IFileEventRepository, FileEventRepository>();
builder.Services.AddSingleton<IDirectoryMonitor, DirectoryMonitor>();
builder.Services.AddSingleton<IFileEventProcessor, FileEventProcessor>();
builder.Services.AddHostedService<FileMonitoringWorker>();

var host = builder.Build();
host.Run();