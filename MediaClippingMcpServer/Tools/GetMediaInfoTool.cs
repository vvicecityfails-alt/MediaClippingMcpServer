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
    private readonly ILicenseService _licenseService;
    private readonly ILogger<GetMediaInfoTool> _logger;

    public GetMediaInfoTool(IMediaClippingService mediaService, ILicenseService licenseService, ILogger<GetMediaInfoTool> logger)
    {
        _mediaService = mediaService;
        _licenseService = licenseService;
        _logger = logger;
    }

    [McpServerTool(Name = "get_media_info", Title = "Get Media Info", ReadOnly = true)]
    public async Task<ToolResult> GetMediaInfoAsync(
        [Description("Path to the media file to analyze")] string inputPath,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MCP Tool: get_media_info called with input={InputPath}", inputPath);

        // Check license for local file operations (free tier feature)
        if (!await _licenseService.IsFeatureEnabledAsync(LicenseFeatures.LocalFileOperations, cancellationToken))
        {
            return new ToolResult(
                Success: false,
                Error: "Local file operations require a valid license."
            );
        }

        var request = new MediaInfoRequest(InputPath: inputPath);
        return await _mediaService.GetMediaInfoAsync(request, cancellationToken);
    }
}