using FFMpegCore;
using MediaClippingMcpServer.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using YoutubeExplode;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace MediaClippingMcpServer.Services;

public class MediaClippingService : IMediaClippingService
{
    private readonly ILogger<MediaClippingService> _logger;
    private readonly YoutubeClient _youtubeClient;

    public MediaClippingService(ILogger<MediaClippingService> logger)
    {
        _logger = logger;
        _youtubeClient = new YoutubeClient();
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

    public async Task<DownloadMediaResult> DownloadMediaAsync(DownloadMediaRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Downloading media from: {Url}", request.Url);

            EnsureOutputDirectory(request.OutputPath);

            if (IsYouTubeUrl(request.Url))
            {
                return await DownloadFromYouTubeAsync(request, cancellationToken);
            }
            else
            {
                return await DownloadWithYtDlpAsync(request, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download media from: {Url}", request.Url);
            return new DownloadMediaResult(false, null, ex.Message, null, null, null);
        }
    }

    private bool IsYouTubeUrl(string url)
    {
        return url.Contains("youtube.com") || url.Contains("youtu.be");
    }

    private async Task<DownloadMediaResult> DownloadFromYouTubeAsync(DownloadMediaRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var videoId = VideoId.TryParse(request.Url);
            if (videoId == null)
            {
                return new DownloadMediaResult(false, null, "Invalid YouTube URL", null, null, null);
            }

            var video = await _youtubeClient.Videos.GetAsync(videoId.Value, cancellationToken);
            var streamManifest = await _youtubeClient.Videos.Streams.GetManifestAsync(videoId.Value, cancellationToken);

            IStreamInfo streamInfo;
            if (request.AudioOnly)
            {
                streamInfo = streamManifest.GetAudioOnlyStreams()
                    .OrderByDescending(s => s.Bitrate)
                    .FirstOrDefault();
            }
            else
            {
                streamInfo = streamManifest.GetVideoStreams()
                    .Where(s => string.IsNullOrEmpty(request.Format) || s.Container.Name.Equals(request.Format, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(s => s.VideoQuality)
                    .FirstOrDefault();

                if (streamInfo == null)
                {
                    streamInfo = streamManifest.GetMuxedStreams()
                        .Where(s => string.IsNullOrEmpty(request.Format) || s.Container.Name.Equals(request.Format, StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(s => s.VideoQuality)
                        .FirstOrDefault();
                }
            }

            if (streamInfo == null)
            {
                return new DownloadMediaResult(false, null, "No suitable stream found", video.Title, video.Duration, video.Thumbnails.FirstOrDefault()?.Url);
            }

            var extension = streamInfo.Container.Name;
            var outputPath = Path.ChangeExtension(request.OutputPath, extension);

            await _youtubeClient.Videos.Streams.DownloadAsync(streamInfo, outputPath);

            _logger.LogInformation("Successfully downloaded YouTube video to: {OutputPath}", outputPath);
            return new DownloadMediaResult(true, outputPath, null, video.Title, video.Duration, video.Thumbnails.FirstOrDefault()?.Url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download from YouTube: {Url}", request.Url);
            return new DownloadMediaResult(false, null, ex.Message, null, null, null);
        }
    }

    private async Task<DownloadMediaResult> DownloadWithYtDlpAsync(DownloadMediaRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var extension = string.IsNullOrEmpty(request.Format) ? "mp4" : request.Format;
            var outputPath = Path.ChangeExtension(request.OutputPath, extension);

            var args = $"-o \"{outputPath}\"";
            if (request.AudioOnly)
            {
                args += " -x --audio-format mp3";
            }
            else if (!string.IsNullOrEmpty(request.Format))
            {
                args += $" --recode-video {request.Format}";
            }
            args += $" \"{request.Url}\"";

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "yt-dlp",
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync();
                return new DownloadMediaResult(false, null, $"yt-dlp failed: {error}", null, null, null);
            }

            // Try to get metadata from yt-dlp
            var metaProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "yt-dlp",
                    Arguments = $"--print \"%(title)s|%(duration)s|%(thumbnail)s\" \"{request.Url}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            metaProcess.Start();
            var metaOutput = await metaProcess.StandardOutput.ReadToEndAsync();
            await metaProcess.WaitForExitAsync(cancellationToken);

            string? title = null, thumbnailUrl = null;
            TimeSpan? duration = null;

            if (!string.IsNullOrWhiteSpace(metaOutput))
            {
                var parts = metaOutput.Trim().Split('|');
                if (parts.Length >= 1) title = parts[0];
                if (parts.Length >= 2 && double.TryParse(parts[1], out var durSec)) duration = TimeSpan.FromSeconds(durSec);
                if (parts.Length >= 3) thumbnailUrl = parts[2];
            }

            _logger.LogInformation("Successfully downloaded with yt-dlp to: {OutputPath}", outputPath);
            return new DownloadMediaResult(true, outputPath, null, title, duration, thumbnailUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download with yt-dlp: {Url}", request.Url);
            return new DownloadMediaResult(false, null, ex.Message, null, null, null);
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