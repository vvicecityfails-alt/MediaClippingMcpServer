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
    private readonly ILicenseService _licenseService;
    private readonly ILogger<DownloadMediaTool> _logger;

    public DownloadMediaTool(IMediaClippingService mediaService, ILicenseService licenseService, ILogger<DownloadMediaTool> logger)
    {
        _mediaService = mediaService;
        _licenseService = licenseService;
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

        // Check license for YouTube/Vimeo download features
        var isYouTube = url.Contains("youtube.com") || url.Contains("youtu.be");
        var isVimeo = url.Contains("vimeo.com");

        string requiredFeature = isYouTube ? LicenseFeatures.YouTubeDownload :
                                 isVimeo ? LicenseFeatures.VimeoDownload :
                                 LicenseFeatures.LocalFileOperations;

        if (!await _licenseService.IsFeatureEnabledAsync(requiredFeature, cancellationToken))
        {
            var tier = await _licenseService.GetCurrentTierAsync(cancellationToken);
            return new DownloadMediaResult(
                Success: false,
                OutputPath: "",
                Error: $"Feature '{requiredFeature}' requires {GetTierName(tier)} tier or higher. " +
                       "Upgrade at https://github.com/sponsors/mukul1603 or use a license key."
            );
        }

        var request = new DownloadMediaRequest(
            Url: url,
            OutputPath: outputPath,
            Format: format,
            AudioOnly: audioOnly
        );

        return await _mediaService.DownloadMediaAsync(request, cancellationToken);
    }

    private static string GetTierName(LicenseTier tier) => tier switch
    {
        LicenseTier.Free => "Pro",
        LicenseTier.Pro => "Enterprise",
        LicenseTier.Enterprise => "Enterprise",
        _ => "Pro"
    };
}