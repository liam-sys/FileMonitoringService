using FileMonitoringService.Options;
using Microsoft.Extensions.Options;
using FileMonitoringService;
using File_Monitor;

var builder = Host.CreateApplicationBuilder(args);

// DAY 2: read the FileMonitoring section of appsettings.json and check it when the service starts (FR-05)
builder.Services.AddSingleton<IValidateOptions<FileMonitoringOptions>, FileMonitoringOptionsValidator>();
builder.Services
    .AddOptions<FileMonitoringOptions>()
    .Bind(builder.Configuration.GetSection(FileMonitoringOptions.SectionName))
    .ValidateOnStart();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
