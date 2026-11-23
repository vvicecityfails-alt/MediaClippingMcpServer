using MediaClippingMcpServer.Models;
using MediaClippingMcpServer.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace MediaClippingMcpServer.Tools;

[McpServerToolType]
public class GetMediaInfoTool
{
    private readonly IMediaClippingService _mediaService;
    private readonly ILogger<GetMediaInfoTool> _logger;

    public GetMediaInfoTool(IMediaClippingService mediaService, ILogger<GetMediaInfoTool> logger)
    {
        _mediaService = mediaService;
        _logger = logger;
    }

    [McpServerTool(Name = "get_media_info", Title = "Get Media Info", ReadOnly = true)]
    public async Task<ToolResult> GetMediaInfoAsync(
        [Description("Path to the media file to analyze")] string inputPath,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MCP Tool: get_media_info called with input={InputPath}", inputPath);

        var request = new MediaInfoRequest(InputPath: inputPath);
        return await _mediaService.GetMediaInfoAsync(request, cancellationToken);
    }
}