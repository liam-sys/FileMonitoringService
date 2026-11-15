using FileMonitoringService.Monitoring;
using FileMonitoringService.Options;
using FileMonitoringService.Worker;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

// DAY 2: read the FileMonitoring settings and check them when the service starts
builder.Services.AddSingleton<IValidateOptions<FileMonitoringOptions>, FileMonitoringOptionsValidator>();
builder.Services
    .AddOptions<FileMonitoringOptions>()
    .Bind(builder.Configuration.GetSection(FileMonitoringOptions.SectionName))
    .ValidateOnStart();

// DAY 4: "when someone asks for IDirectoryMonitor, give them a DirectoryMonitor",
// and run our worker in the background
builder.Services.AddSingleton<IDirectoryMonitor, DirectoryMonitor>();
builder.Services.AddHostedService<FileMonitoringWorker>();

var host = builder.Build();
host.Run();