using MediaClippingMcpServer.Services;
using MediaClippingMcpServer.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(configure => configure.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<ClipMediaTool>()
    .WithTools<ExtractAudioTool>()
    .WithTools<ConvertMediaTool>()
    .WithTools<GetMediaInfoTool>()
    .WithTools<DownloadMediaTool>();

builder.Services.AddSingleton<IMediaClippingService, MediaClippingService>();

var app = builder.Build();

app.Run();