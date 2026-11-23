using MediaClippingMcpServer.Models;
using MediaClippingMcpServer.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace MediaClippingMcpServer.Tools;

[McpServerToolType]
public class DownloadMediaTool
{
    private readonly IMediaClippingService _mediaService;
    private readonly ILogger<DownloadMediaTool> _logger;

    public DownloadMediaTool(IMediaClippingService mediaService, ILogger<DownloadMediaTool> logger)
    {
        _mediaService = mediaService;
        _logger = logger;
    }

    [McpServerTool(Name = "download_media", Title = "Download Media", ReadOnly = false, Destructive = false)]
    public async Task<DownloadMediaResult> DownloadMediaAsync(
        [Description("URL to download (YouTube, Vimeo, Twitter, etc.)")] string url,
        [Description("Output file path (extension will be adjusted based on format)")] string outputPath,
        [Description("Format preference: best, mp4, webm, mkv, mp3, m4a, etc.")] string? format = null,
        [Description("Download audio only (extracts audio track)")] bool audioOnly = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MCP Tool: download_media called with url={Url}", url);

        var request = new DownloadMediaRequest(
            Url: url,
            OutputPath: outputPath,
            Format: format,
            AudioOnly: audioOnly
        );

        return await _mediaService.DownloadMediaAsync(request, cancellationToken);
    }
}