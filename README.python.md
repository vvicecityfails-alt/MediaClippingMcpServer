# Media Clipping MCP Server (Python)

[![GitHub Sponsors](https://img.shields.io/github/sponsors/mukul1603?style=social)](https://github.com/sponsors/mukul1603)
[![Patreon](https://img.shields.io/badge/Patreon-Support%20Me-F96854?logo=patreon&logoColor=white)](https://patreon.com/mukul1603)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Python 3.11+](https://img.shields.io/badge/Python-3.11%2B-blue?logo=python)](https://python.org/)
[![PyPI](https://img.shields.io/pypi/v/media-clipping-mcp?logo=pypi)](https://pypi.org/project/media-clipping-mcp)
[![Docker](https://img.shields.io/badge/docker-ghcr.io-blue?logo=docker)](https://github.com/mukul1603/MediaClippingMcpServer/pkgs/container/media-clipping-mcp)

An MCP (Model Context Protocol) server that provides video and audio clipping capabilities using FFmpeg. Built with Python 3.11+.

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

- **Python 3.11+**
- [FFmpeg](https://ffmpeg.org/download.html) installed and in PATH
- [yt-dlp](https://github.com/yt-dlp/yt-dlp) for non-YouTube URLs (optional but recommended)

### Option 1: PyPI Package (Recommended)

```bash
# Install from PyPI
pip install media-clipping-mcp

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

### Option 3: From Source

```bash
git clone https://github.com/mukul1603/MediaClippingMcpServer.git
cd MediaClippingMcpServer
pip install -e .
media-clip
```

### Option 4: Development Install

```bash
git clone https://github.com/mukul1603/MediaClippingMcpServer.git
cd MediaClippingMcpServer
pip install -e .[dev]
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
3. **Environment config**: Configure `LICENSE_PUBLIC_KEY` and `LICENSE_STRIPE_SECRET_KEY` for automatic Stripe validation

Get your license key by [subscribing on GitHub Sponsors](https://github.com/sponsors/mukul1603) or [purchasing a license](https://github.com/mukul1603/MediaClippingMcpServer#pricing).

### Available Tools

| Tool | Description | Parameters | Tier |
|------|-------------|------------|------|
| `download_media` | Download from URL (YouTube, Vimeo, Twitter, etc.) | `url`, `outputPath`, `format?`, `audioOnly?` | Pro+ |
| `clip_media` | Trim media by timeframe | `inputPath`, `outputPath`, `startTime`, `endTime?`, `duration?` | Free |
| `extract_audio` | Extract audio from video | `inputPath`, `outputPath`, `audioCodec?`, `bitrate?` | Free |
| `convert_media` | Convert media format | `inputPath`, `outputPath`, `videoCodec?`, `audioCodec?`, `preset?` | Free |
| `get_media_info` | Get media metadata | `inputPath` | Free |

### Time Format

Times can be specified as:
- `HH:MM:SS` (e.g., `00:01:30`)
- Seconds as number (e.g., `90` or `90.5`)

## Examples

### Download a YouTube video (Pro+)
```json
{
  "tool": "download_media",
  "arguments": {
    "url": "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
    "outputPath": "/path/to/video.mp4"
  }
}
```

### Download audio only from YouTube (Pro+)
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

### Download from other sites (Pro+, requires yt-dlp)
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

### Clip a video from 30s to 90s (Free)
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

### Extract audio as MP3 (Free)
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

### Convert to H.265 with medium preset (Free)
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
# Install dependencies
pip install -e .[dev]

# Run server (for testing with MCP Inspector)
media-clip

# Run tests
pytest

# Lint
ruff check src/
mypy src/

# Generate license keys
python -m media_clipping_mcp.license_gen --generate-keypair
python -m media_clipping_mcp.license_gen --private-key license_private_key.pem --tier 1 --email user@example.com
```

## Architecture

```
media_clipping_mcp/
├── server.py              # MCP server entry point
├── config.py              # Configuration management
├── license.py             # License validation & feature gating
├── tools.py               # Media clipping tools (FFmpeg + yt-dlp)
├── license_gen.py         # License key generator (RSA signing)
└── __init__.py
```

## Dependencies

- [MCP Python SDK](https://github.com/modelcontextprotocol/python-sdk) - Official MCP SDK for Python
- [ffmpeg-python](https://github.com/kkroening/ffmpeg-python) - Python FFmpeg wrapper
- [yt-dlp](https://github.com/yt-dlp/yt-dlp) - Video downloading from 1000+ sites
- [cryptography](https://cryptography.io/) - RSA license signing/verification
- [pydantic-settings](https://docs.pydantic.dev/latest/concepts/pydantic_settings/) - Configuration management

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