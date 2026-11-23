using MediaClippingMcpServer.Models;
using MediaClippingMcpServer.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace MediaClippingMcpServer.Tools;

[McpServerToolType]
public class ConvertMediaTool
{
    private readonly IMediaClippingService _mediaService;
    private readonly ILogger<ConvertMediaTool> _logger;

    public ConvertMediaTool(IMediaClippingService mediaService, ILogger<ConvertMediaTool> logger)
    {
        _mediaService = mediaService;
        _logger = logger;
    }

    [McpServerTool(Name = "convert_media", Title = "Convert Media", ReadOnly = false, Destructive = false)]
    public async Task<ToolResult> ConvertMediaAsync(
        [Description("Path to the input media file")] string inputPath,
        [Description("Path for the output converted media file")] string outputPath,
        [Description("Video codec to use (e.g., libx264, libx265, libvpx-vp9)")] string? videoCodec = null,
        [Description("Audio codec to use (e.g., aac, libmp3lame, opus)")] string? audioCodec = null,
        [Description("Encoding preset (e.g., ultrafast, fast, medium, slow, veryslow)")] string? preset = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MCP Tool: convert_media called with input={InputPath}", inputPath);

        var request = new ConvertMediaRequest(
            InputPath: inputPath,
            OutputPath: outputPath,
            VideoCodec: videoCodec,
            AudioCodec: audioCodec,
            Preset: preset
        );

        return await _mediaService.ConvertMediaAsync(request, cancellationToken);
    }
}