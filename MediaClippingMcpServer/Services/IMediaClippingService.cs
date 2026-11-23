using MediaClippingMcpServer.Models;

namespace MediaClippingMcpServer.Services;

public interface IMediaClippingService
{
    Task<ToolResult> ClipMediaAsync(ClipRequest request, CancellationToken cancellationToken = default);
    Task<ToolResult> ExtractAudioAsync(ExtractAudioRequest request, CancellationToken cancellationToken = default);
    Task<ToolResult> ConvertMediaAsync(ConvertMediaRequest request, CancellationToken cancellationToken = default);
    Task<ToolResult> GetMediaInfoAsync(MediaInfoRequest request, CancellationToken cancellationToken = default);
    Task<DownloadMediaResult> DownloadMediaAsync(DownloadMediaRequest request, CancellationToken cancellationToken = default);
}