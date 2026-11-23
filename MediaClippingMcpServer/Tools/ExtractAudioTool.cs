using MediaClippingMcpServer.Models;
using MediaClippingMcpServer.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace MediaClippingMcpServer.Tools;

[McpServerToolType]
public class ExtractAudioTool
{
    private readonly IMediaClippingService _mediaService;
    private readonly ILogger<ExtractAudioTool> _logger;

    public ExtractAudioTool(IMediaClippingService mediaService, ILogger<ExtractAudioTool> logger)
    {
        _mediaService = mediaService;
        _logger = logger;
    }

    [McpServerTool(Name = "extract_audio", Title = "Extract Audio", ReadOnly = false, Destructive = false)]
    public async Task<ToolResult> ExtractAudioAsync(
        [Description("Path to the input media file")] string inputPath,
        [Description("Path for the output audio file")] string outputPath,
        [Description("Audio codec to use (e.g., libmp3lame, aac, opus, flac)")] string? audioCodec = null,
        [Description("Audio bitrate (e.g., 192k, 320k)")] string? bitrate = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MCP Tool: extract_audio called with input={InputPath}", inputPath);

        var request = new ExtractAudioRequest(
            InputPath: inputPath,
            OutputPath: outputPath,
            AudioCodec: audioCodec,
            Bitrate: bitrate
        );

        return await _mediaService.ExtractAudioAsync(request, cancellationToken);
    }
}