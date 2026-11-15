using FileMonitoringService.Monitoring;
using FileMonitoringService.Options;
using FileMonitoringService.Services;
using FileMonitoringService.Worker;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

// DAY 2: read the FileMonitoring settings and check them when the service starts
builder.Services.AddSingleton<IValidateOptions<FileMonitoringOptions>, FileMonitoringOptionsValidator>();
builder.Services
    .AddOptions<FileMonitoringOptions>()
    .Bind(builder.Configuration.GetSection(FileMonitoringOptions.SectionName))
    .ValidateOnStart();

// DAY 4 and 5: "when someone asks for the interface, give them the real class"
builder.Services.AddSingleton<IDirectoryMonitor, DirectoryMonitor>();
builder.Services.AddSingleton<IFileEventProcessor, FileEventProcessor>();
builder.Services.AddHostedService<FileMonitoringWorker>();

var host = builder.Build();
host.Run();