# Media Clipping MCP Server

[![GitHub Sponsors](https://img.shields.io/github/sponsors/mukul1603?style=social)](https://github.com/sponsors/mukul1603)
[![Patreon](https://img.shields.io/badge/Patreon-Support%20Me-F96854?logo=patreon&logoColor=white)](https://patreon.com/mukul1603)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![NuGet](https://img.shields.io/nuget/v/MediaClippingMcpServer?logo=nuget)](https://www.nuget.org/packages/MediaClippingMcpServer)
[![Docker](https://img.shields.io/badge/docker-ghcr.io-blue?logo=docker)](https://github.com/mukul1603/MediaClippingMcpServer/pkgs/container/media-clipping-mcp)

An MCP (Model Context Protocol) server that provides video and audio clipping capabilities using FFmpeg. Built with C#/.NET 10.

## Features

| Feature | Free | Pro | Enterprise |
|---------|:----:|:---:|:----------:|
| **Local File Operations** (clip, extract, convert, info) | ✅ | ✅ | ✅ |
| **YouTube Download** | ❌ | ✅ | ✅ |
| **Vimeo/Twitter/1000+ sites** | ❌ | ✅ | ✅ |
| **Batch Processing** | ❌ | ✅ | ✅ |
| **Priority Queue** | ❌ | ✅ | ✅ |
| **Custom Tools** | ❌ | ❌ | ✅ |
| **SSO / Audit Logs / SLA** | ❌ | ❌ | ✅ |

## Installation

### Prerequisites

- [FFmpeg](https://ffmpeg.org/download.html) installed and in PATH
- [yt-dlp](https://github.com/yt-dlp/yt-dlp) for non-YouTube URLs (optional but recommended)

### Option 1: .NET Global Tool (Recommended)

```bash
# Install from NuGet
dotnet tool install --global MediaClippingMcpServer

# Run
media-clip
```

### Option 2: Docker (Cross-platform)

```bash
# Pull from GHCR
docker pull ghcr.io/mukul1603/media-clipping-mcp:latest

# Run (stdio transport for MCP)
docker run -i --rm ghcr.io/mukul1603/media-clipping-mcp:latest
```

### Option 3: Standalone Binaries

Download from [GitHub Releases](https://github.com/mukul1603/MediaClippingMcpServer/releases):
- `media-clip-win-x64.tar.gz` - Windows x64
- `media-clip-linux-x64.tar.gz` - Linux x64
- `media-clip-osx-x64.tar.gz` - macOS x64
- `media-clip-osx-arm64.tar.gz` - macOS ARM64 (Apple Silicon)

```bash
# Extract and run
tar -xzf media-clip-linux-x64.tar.gz
./MediaClippingMcpServer
```

### Option 4: From Source

```bash
git clone https://github.com/mukul1603/MediaClippingMcpServer.git
cd MediaClippingMcpServer
dotnet build -c Release
dotnet run --project MediaClippingMcpServer/MediaClippingMcpServer.csproj
```

## Usage

### With Claude Desktop

**Free Tier (Local operations only):**

```json
{
  "mcpServers": {
    "media-clipping": {
      "command": "media-clip",
      "args": [],
      "env": {}
    }
  }
}
```

**Pro/Enterprise Tier (with license key):**

```json
{
  "mcpServers": {
    "media-clipping": {
      "command": "media-clip",
      "args": [],
      "env": {
        "MEDIA_CLIP_LICENSE_KEY": "your-license-key-here"
      }
    }
  }
}
```

**Using Docker:**

```json
{
  "mcpServers": {
    "media-clipping": {
      "command": "docker",
      "args": ["run", "-i", "--rm", "ghcr.io/mukul1603/media-clipping-mcp:latest"],
      "env": {
        "MEDIA_CLIP_LICENSE_KEY": "your-license-key-here"
      }
    }
  }
}
```

### License Configuration

Licenses can be provided via:
1. **Environment variable** (recommended): `MEDIA_CLIP_LICENSE_KEY`
2. **License file**: Place `license.json` in the working directory
3. **appsettings.json**: Configure `License:StripeSecretKey` for automatic Stripe validation

Get your license key by [subscribing on GitHub Sponsors](https://github.com/sponsors/mukul1603) or [purchasing a license](https://github.com/mukul1603/MediaClippingMcpServer#pricing).

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

## Pricing

| Tier | Price | Best For |
|------|-------|----------|
| **Free** | $0 | Personal use, local files only |
| **Pro** | $10/mo | Content creators, developers needing YouTube/Vimeo download |
| **Enterprise** | Custom | Teams needing SSO, audit logs, SLA, custom tools |

[**Subscribe on GitHub Sponsors →**](https://github.com/sponsors/mukul1603)

## Monetization

This project uses an **Open Core** model:
- **Core (MIT)**: All local file operations free forever
- **Pro Features**: YouTube/Vimeo download, batch processing require subscription
- **Revenue funds**: Development, testing, FFmpeg/yt-dlp compatibility, support

Support development:
- **GitHub Sponsors**: [github.com/sponsors/mukul1603](https://github.com/sponsors/mukul1603) (zero fees)
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