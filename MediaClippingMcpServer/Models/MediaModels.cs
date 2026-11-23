using System.Text.Json.Serialization;

namespace MediaClippingMcpServer.Models;

public record ClipRequest(
    [property: JsonPropertyName("inputPath")] string InputPath,
    [property: JsonPropertyName("outputPath")] string OutputPath,
    [property: JsonPropertyName("startTime")] TimeSpan StartTime,
    [property: JsonPropertyName("endTime")] TimeSpan? EndTime = null,
    [property: JsonPropertyName("duration")] TimeSpan? Duration = null
);

public record ExtractAudioRequest(
    [property: JsonPropertyName("inputPath")] string InputPath,
    [property: JsonPropertyName("outputPath")] string OutputPath,
    [property: JsonPropertyName("audioCodec")] string? AudioCodec = null,
    [property: JsonPropertyName("bitrate")] string? Bitrate = null
);

public record ConvertMediaRequest(
    [property: JsonPropertyName("inputPath")] string InputPath,
    [property: JsonPropertyName("outputPath")] string OutputPath,
    [property: JsonPropertyName("videoCodec")] string? VideoCodec = null,
    [property: JsonPropertyName("audioCodec")] string? AudioCodec = null,
    [property: JsonPropertyName("preset")] string? Preset = null
);

public record MediaInfoRequest(
    [property: JsonPropertyName("inputPath")] string InputPath
);

public record MediaInfo(
    [property: JsonPropertyName("duration")] TimeSpan Duration,
    [property: JsonPropertyName("size")] long SizeBytes,
    [property: JsonPropertyName("videoCodec")] string? VideoCodec,
    [property: JsonPropertyName("audioCodec")] string? AudioCodec,
    [property: JsonPropertyName("width")] int? Width,
    [property: JsonPropertyName("height")] int? Height,
    [property: JsonPropertyName("frameRate")] double? FrameRate,
    [property: JsonPropertyName("bitrate")] long? Bitrate,
    [property: JsonPropertyName("format")] string Format
);

public record ToolResult(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("outputPath")] string? OutputPath = null,
    [property: JsonPropertyName("error")] string? Error = null,
    [property: JsonPropertyName("mediaInfo")] MediaInfo? MediaInfo = null
);