using FFMpegCore;
using MediaClippingMcpServer.Models;
using Microsoft.Extensions.Logging;

namespace MediaClippingMcpServer.Services;

public class MediaClippingService : IMediaClippingService
{
    private readonly ILogger<MediaClippingService> _logger;

    public MediaClippingService(ILogger<MediaClippingService> logger)
    {
        _logger = logger;
    }

    public async Task<ToolResult> ClipMediaAsync(ClipRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Clipping media: {InputPath} from {StartTime} to {EndTime}",
                request.InputPath, request.StartTime, request.EndTime ?? request.StartTime + (request.Duration ?? TimeSpan.Zero));

            ValidateInputFile(request.InputPath);
            EnsureOutputDirectory(request.OutputPath);

            var ffmpeg = FFMpegArguments
                .FromFileInput(request.InputPath)
                .OutputToFile(request.OutputPath, true, options => options
                    .Seek(request.StartTime)
                    .WithCustomArgument(GetDurationArgument(request.EndTime, request.Duration))
                    .WithFastStart());

            await ffmpeg.ProcessAsynchronously();

            var mediaInfo = await GetMediaInfoInternalAsync(request.OutputPath, cancellationToken);

            _logger.LogInformation("Successfully clipped media to: {OutputPath}", request.OutputPath);
            return new ToolResult(true, request.OutputPath, null, mediaInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clip media: {InputPath}", request.InputPath);
            return new ToolResult(false, null, ex.Message, null);
        }
    }

    public async Task<ToolResult> ExtractAudioAsync(ExtractAudioRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Extracting audio from: {InputPath} to {OutputPath}", request.InputPath, request.OutputPath);

            ValidateInputFile(request.InputPath);
            EnsureOutputDirectory(request.OutputPath);

            var ffmpeg = FFMpegArguments
                .FromFileInput(request.InputPath)
                .OutputToFile(request.OutputPath, true, options => options
                    .WithAudioCodec(request.AudioCodec ?? "libmp3lame")
                    .WithCustomArgument($"-b:a {request.Bitrate ?? "192k"}")
                    .WithVideoCodec("none")
                    .WithFastStart());

            await ffmpeg.ProcessAsynchronously();

            var mediaInfo = await GetMediaInfoInternalAsync(request.OutputPath, cancellationToken);

            _logger.LogInformation("Successfully extracted audio to: {OutputPath}", request.OutputPath);
            return new ToolResult(true, request.OutputPath, null, mediaInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract audio: {InputPath}", request.InputPath);
            return new ToolResult(false, null, ex.Message, null);
        }
    }

    public async Task<ToolResult> ConvertMediaAsync(ConvertMediaRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Converting media: {InputPath} to {OutputPath}", request.InputPath, request.OutputPath);

            ValidateInputFile(request.InputPath);
            EnsureOutputDirectory(request.OutputPath);

            var ffmpeg = FFMpegArguments
                .FromFileInput(request.InputPath)
                .OutputToFile(request.OutputPath, true, options =>
                {
                    if (!string.IsNullOrEmpty(request.VideoCodec))
                        options.WithVideoCodec(request.VideoCodec);

                    if (!string.IsNullOrEmpty(request.AudioCodec))
                        options.WithAudioCodec(request.AudioCodec);

                    if (!string.IsNullOrEmpty(request.Preset))
                        options.WithCustomArgument($"-preset {request.Preset}");

                    options.WithFastStart();
                });

            await ffmpeg.ProcessAsynchronously();

            var mediaInfo = await GetMediaInfoInternalAsync(request.OutputPath, cancellationToken);

            _logger.LogInformation("Successfully converted media to: {OutputPath}", request.OutputPath);
            return new ToolResult(true, request.OutputPath, null, mediaInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to convert media: {InputPath}", request.InputPath);
            return new ToolResult(false, null, ex.Message, null);
        }
    }

    public async Task<ToolResult> GetMediaInfoAsync(MediaInfoRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting media info for: {InputPath}", request.InputPath);

            ValidateInputFile(request.InputPath);

            var mediaInfo = await GetMediaInfoInternalAsync(request.InputPath, cancellationToken);

            _logger.LogInformation("Successfully retrieved media info for: {InputPath}", request.InputPath);
            return new ToolResult(true, null, null, mediaInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get media info: {InputPath}", request.InputPath);
            return new ToolResult(false, null, ex.Message, null);
        }
    }

    private async Task<MediaInfo> GetMediaInfoInternalAsync(string filePath, CancellationToken cancellationToken)
    {
        var mediaAnalysis = await FFProbe.AnalyseAsync(filePath);
        var primaryVideoStream = mediaAnalysis.PrimaryVideoStream;
        var primaryAudioStream = mediaAnalysis.PrimaryAudioStream;

        return new MediaInfo(
            Duration: mediaAnalysis.Duration,
            SizeBytes: new FileInfo(filePath).Length,
            VideoCodec: primaryVideoStream?.CodecName,
            AudioCodec: primaryAudioStream?.CodecName,
            Width: primaryVideoStream?.Width,
            Height: primaryVideoStream?.Height,
            FrameRate: primaryVideoStream?.FrameRate,
            Bitrate: null,
            Format: mediaAnalysis.Format?.ToString() ?? Path.GetExtension(filePath).TrimStart('.')
        );
    }

    private static string GetDurationArgument(TimeSpan? endTime, TimeSpan? duration)
    {
        if (endTime.HasValue)
            return $"-to {endTime.Value.TotalSeconds}";

        if (duration.HasValue)
            return $"-t {duration.Value.TotalSeconds}";

        return "";
    }

    private static void ValidateInputFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Input path cannot be empty", nameof(path));

        if (!File.Exists(path))
            throw new FileNotFoundException($"Input file not found: {path}", path);
    }

    private static void EnsureOutputDirectory(string outputPath)
    {
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}