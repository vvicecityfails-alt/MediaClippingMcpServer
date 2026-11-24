using MediaClippingMcpServer.Services;
using MediaClippingMcpServer.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(configure => configure.LogToStandardErrorThreshold = LogLevel.Trace);

// Configure license service options
builder.Services.Configure<LicenseServiceOptions>(builder.Configuration.GetSection(LicenseServiceOptions.SectionName));

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<ClipMediaTool>()
    .WithTools<ExtractAudioTool>()
    .WithTools<ConvertMediaTool>()
    .WithTools<GetMediaInfoTool>()
    .WithTools<DownloadMediaTool>();

builder.Services.AddSingleton<IMediaClippingService, MediaClippingService>();
builder.Services.AddSingleton<ILicenseService, LicenseService>();
builder.Services.AddHttpClient<LicenseService>();

var app = builder.Build();

app.Run();