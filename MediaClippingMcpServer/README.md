# Media Clipping MCP Server

[![GitHub Sponsors](https://img.shields.io/github/sponsors/mukul1603?style=social)](https://github.com/sponsors/mukul1603)
[![Patreon](https://img.shields.io/badge/Patreon-Support%20Me-F96854?logo=patreon&logoColor=white)](https://patreon.com/mukul1603)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)

An MCP (Model Context Protocol) server that provides video and audio clipping capabilities using FFmpeg. Built with C#/.NET 10.

## Features

- **Download Media** - Download videos from YouTube, Vimeo, Twitter, and 1000+ sites (via yt-dlp)
- **Clip Media** - Trim video/audio by start/end time or duration
- **Extract Audio** - Extract audio track from video files
- **Convert Media** - Convert between formats with codec/preset control
- **Get Media Info** - Analyze media files (duration, codecs, resolution, bitrate)

## Installation

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [FFmpeg](https://ffmpeg.org/download.html) installed and in PATH
- [yt-dlp](https://github.com/yt-dlp/yt-dlp) for non-YouTube URLs (optional but recommended)

### From Source

```bash
git clone https://github.com/mukul1603/MediaClippingMcpServer.git
cd MediaClippingMcpServer
dotnet build -c Release
```

### As a .NET Tool (Planned)

```bash
dotnet tool install --global MediaClippingMcpServer
```

## Usage

### With Claude Desktop

Add to your `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "media-clipping": {
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/MediaClippingMcpServer/MediaClippingMcpServer.csproj"],
      "env": {}
    }
  }
}
```

Or if installed as a tool:

```json
{
  "mcpServers": {
    "media-clipping": {
      "command": "MediaClippingMcpServer",
      "args": [],
      "env": {}
    }
  }
}
```

### Available Tools

| Tool | Description | Parameters |
|------|-------------|------------|
| `download_media` | Download from URL (YouTube, Vimeo, Twitter, etc.) | `url`, `outputPath`, `format?`, `audioOnly?` |
| `clip_media` | Trim media by timeframe | `inputPath`, `outputPath`, `startTime`, `endTime?`, `duration?` |
| `extract_audio` | Extract audio from video | `inputPath`, `outputPath`, `audioCodec?`, `bitrate?` |
| `convert_media` | Convert media format | `inputPath`, `outputPath`, `videoCodec?`, `audioCodec?`, `preset?` |
| `get_media_info` | Get media metadata | `inputPath` |

### Time Format

Times can be specified as:
- `HH:MM:SS` (e.g., `00:01:30`)
- Seconds as number (e.g., `90` or `90.5`)

## Examples

### Download a YouTube video
```json
{
  "tool": "download_media",
  "arguments": {
    "url": "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
    "outputPath": "/path/to/video.mp4"
  }
}
```

### Download audio only from YouTube
```json
{
  "tool": "download_media",
  "arguments": {
    "url": "https://youtu.be/dQw4w9WgXcQ",
    "outputPath": "/path/to/audio.mp3",
    "audioOnly": true
  }
}
```

### Download from other sites (requires yt-dlp)
```json
{
  "tool": "download_media",
  "arguments": {
    "url": "https://vimeo.com/123456789",
    "outputPath": "/path/to/video.mp4",
    "format": "mp4"
  }
}
```

### Clip a video from 30s to 90s
```json
{
  "tool": "clip_media",
  "arguments": {
    "inputPath": "/path/to/video.mp4",
    "outputPath": "/path/to/clip.mp4",
    "startTime": "00:00:30",
    "endTime": "00:01:30"
  }
}
```

### Extract audio as MP3
```json
{
  "tool": "extract_audio",
  "arguments": {
    "inputPath": "/path/to/video.mp4",
    "outputPath": "/path/to/audio.mp3",
    "audioCodec": "libmp3lame",
    "bitrate": "192k"
  }
}
```

### Convert to H.265 with medium preset
```json
{
  "tool": "convert_media",
  "arguments": {
    "inputPath": "/path/to/video.mp4",
    "outputPath": "/path/to/video.hevc.mp4",
    "videoCodec": "libx265",
    "preset": "medium"
  }
}
```

## Development

```bash
# Restore dependencies
dotnet restore

# Build
dotnet build

# Run (for testing with MCP Inspector)
dotnet run

# Run tests
dotnet test
```

## Architecture

```
MediaClippingMcpServer/
├── Program.cs                 # MCP server entry point
├── Services/
│   ├── IMediaClippingService.cs
│   └── MediaClippingService.cs  # FFmpeg + YouTube/yt-dlp wrapper
├── Tools/
│   ├── ClipMediaTool.cs
│   ├── ExtractAudioTool.cs
│   ├── ConvertMediaTool.cs
│   ├── GetMediaInfoTool.cs
│   └── DownloadMediaTool.cs
├── Models/
│   └── MediaModels.cs
└── MediaClippingMcpServer.csproj
```

## Dependencies

- [ModelContextProtocol](https://www.nuget.org/packages/ModelContextProtocol) - Official MCP SDK for .NET
- [FFMpegCore](https://www.nuget.org/packages/FFMpegCore) - Cross-platform FFmpeg wrapper
- [YoutubeExplode](https://www.nuget.org/packages/YoutubeExplode) - YouTube video downloading
- Microsoft.Extensions.Hosting / Logging
- [yt-dlp](https://github.com/yt-dlp/yt-dlp) - External tool for non-YouTube sites (optional)

## Monetization

This project is free and open source. If you find it useful, consider supporting development:

- **GitHub Sponsors**: [github.com/sponsors/mukul1603](https://github.com/sponsors/mukul1603)
- **Patreon**: [patreon.com/mukul1603](https://patreon.com/mukul1603)

## License

MIT License - see [LICENSE](LICENSE) for details.

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Run tests and ensure build passes
5. Submit a pull request

## Support

- Open an issue for bugs or feature requests
- Check existing issues before creating new ones
- Provide reproduction steps for bugs