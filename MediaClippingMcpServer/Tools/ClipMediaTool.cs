using MediaClippingMcpServer.Models;
using MediaClippingMcpServer.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace MediaClippingMcpServer.Tools;

[McpServerToolType]
public class ClipMediaTool
{
    private readonly IMediaClippingService _mediaService;
    private readonly ILogger<ClipMediaTool> _logger;

    public ClipMediaTool(IMediaClippingService mediaService, ILogger<ClipMediaTool> logger)
    {
        _mediaService = mediaService;
        _logger = logger;
    }

    [McpServerTool(Name = "clip_media", Title = "Clip Media", ReadOnly = false, Destructive = false)]
    public async Task<ToolResult> ClipMediaAsync(
        [Description("Path to the input media file")] string inputPath,
        [Description("Path for the output clipped media file")] string outputPath,
        [Description("Start time for the clip (format: HH:MM:SS or seconds)")] string startTime,
        [Description("End time for the clip (format: HH:MM:SS or seconds)")] string? endTime = null,
        [Description("Duration of the clip (format: HH:MM:SS or seconds) - alternative to endTime")] string? duration = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MCP Tool: clip_media called with input={InputPath}", inputPath);

        var request = new ClipRequest(
            InputPath: inputPath,
            OutputPath: outputPath,
            StartTime: ParseTimeSpan(startTime),
            EndTime: endTime != null ? ParseTimeSpan(endTime) : null,
            Duration: duration != null ? ParseTimeSpan(duration) : null
        );

        return await _mediaService.ClipMediaAsync(request, cancellationToken);
    }

    private static TimeSpan ParseTimeSpan(string value)
    {
        if (double.TryParse(value, out var seconds))
        {
            return TimeSpan.FromSeconds(seconds);
        }

        if (TimeSpan.TryParse(value, out var timeSpan))
        {
            return timeSpan;
        }

        throw new ArgumentException($"Invalid time format: {value}. Use HH:MM:SS or seconds.", nameof(value));
    }
}