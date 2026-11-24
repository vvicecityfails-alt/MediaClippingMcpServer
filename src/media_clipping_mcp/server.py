"""MCP Server for Media Clipping."""

import asyncio
import logging
import sys
from typing import Any

import mcp.server.stdio
import mcp.types as types
from mcp.server import NotificationOptions, Server
from mcp.server.models import InitializationOptions

from .config import get_config
from .license import LicenseService
from .tools import MediaClippingService, MediaTools

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s - %(name)s - %(levelname)s - %(message)s",
    stream=sys.stderr,
)
logger = logging.getLogger(__name__)


class MediaClippingMCPServer:
    """MCP Server exposing media clipping tools."""

    def __init__(self):
        self.config = get_config()
        self.license_service = LicenseService(self.config.license)
        self.media_service = MediaClippingService(self.config)
        self.tools = MediaTools(self.media_service, self.license_service)

        self.server = Server("media-clipping-mcp")
        self._register_tools()

    def _register_tools(self):
        """Register MCP tools."""

        @self.server.list_tools()
        async def list_tools() -> list[types.Tool]:
            license = self.license_service.get_license()
            tier = license.tier

            tools = [
                types.Tool(
                    name="get_media_info",
                    description="Get media file metadata (duration, codecs, dimensions, etc.)",
                    inputSchema={
                        "type": "object",
                        "properties": {
                            "inputPath": {"type": "string", "description": "Path to media file"},
                        },
                        "required": ["inputPath"],
                    },
                ),
                types.Tool(
                    name="clip_media",
                    description="Trim media by timeframe (start/end time or duration)",
                    inputSchema={
                        "type": "object",
                        "properties": {
                            "inputPath": {"type": "string", "description": "Path to input media file"},
                            "outputPath": {"type": "string", "description": "Path for output file"},
                            "startTime": {"type": "string", "description": "Start time (HH:MM:SS or seconds)"},
                            "endTime": {"type": "string", "description": "End time (HH:MM:SS or seconds)"},
                            "duration": {"type": "string", "description": "Duration (HH:MM:SS or seconds)"},
                        },
                        "required": ["inputPath", "outputPath", "startTime"],
                    },
                ),
                types.Tool(
                    name="extract_audio",
                    description="Extract audio track from video file",
                    inputSchema={
                        "type": "object",
                        "properties": {
                            "inputPath": {"type": "string", "description": "Path to input video file"},
                            "outputPath": {"type": "string", "description": "Path for output audio file"},
                            "audioCodec": {"type": "string", "description": "Audio codec (default: libmp3lame)", "default": "libmp3lame"},
                            "bitrate": {"type": "string", "description": "Audio bitrate (default: 192k)", "default": "192k"},
                        },
                        "required": ["inputPath", "outputPath"],
                    },
                ),
                types.Tool(
                    name="convert_media",
                    description="Convert media to different format/codec",
                    inputSchema={
                        "type": "object",
                        "properties": {
                            "inputPath": {"type": "string", "description": "Path to input media file"},
                            "outputPath": {"type": "string", "description": "Path for output file"},
                            "videoCodec": {"type": "string", "description": "Video codec (e.g., libx264, libx265)"},
                            "audioCodec": {"type": "string", "description": "Audio codec (e.g., aac, libmp3lame)"},
                            "preset": {"type": "string", "description": "FFmpeg preset (e.g., fast, medium, slow)"},
                        },
                        "required": ["inputPath", "outputPath"],
                    },
                ),
            ]

            # Pro/Enterprise tools
            if tier >= LicenseTier.PRO:
                tools.append(
                    types.Tool(
                        name="download_media",
                        description="Download media from URL (YouTube, Vimeo, Twitter, 1000+ sites)",
                        inputSchema={
                            "type": "object",
                            "properties": {
                                "url": {"type": "string", "description": "Media URL to download"},
                                "outputPath": {"type": "string", "description": "Path for output file"},
                                "format": {"type": "string", "description": "Output format (mp4, mp3, etc.)"},
                                "audioOnly": {"type": "boolean", "description": "Download audio only", "default": False},
                            },
                            "required": ["url", "outputPath"],
                        },
                    )
                )

            return tools

        @self.server.call_tool()
        async def call_tool(name: str, arguments: dict[str, Any]) -> list[types.TextContent]:
            try:
                if name == "get_media_info":
                    result = self.tools.get_media_info(arguments["inputPath"])
                elif name == "clip_media":
                    result = self.tools.clip_media(
                        arguments["inputPath"],
                        arguments["outputPath"],
                        arguments["startTime"],
                        arguments.get("endTime"),
                        arguments.get("duration"),
                    )
                elif name == "extract_audio":
                    result = self.tools.extract_audio(
                        arguments["inputPath"],
                        arguments["outputPath"],
                        arguments.get("audioCodec", "libmp3lame"),
                        arguments.get("bitrate", "192k"),
                    )
                elif name == "convert_media":
                    result = self.tools.convert_media(
                        arguments["inputPath"],
                        arguments["outputPath"],
                        arguments.get("videoCodec"),
                        arguments.get("audioCodec"),
                        arguments.get("preset"),
                    )
                elif name == "download_media":
                    result = await self.tools.download_media(
                        arguments["url"],
                        arguments["outputPath"],
                        arguments.get("format"),
                        arguments.get("audioOnly", False),
                    )
                else:
                    raise ValueError(f"Unknown tool: {name}")

                return [types.TextContent(type="text", text=json.dumps(result, indent=2))]

            except PermissionError as e:
                return [types.TextContent(type="text", text=json.dumps({"error": str(e)}, indent=2))]
            except Exception as e:
                logger.exception(f"Tool {name} failed")
                return [types.TextContent(type="text", text=json.dumps({"error": str(e)}, indent=2))]

    async def run(self):
        """Run the MCP server."""
        async with mcp.server.stdio.stdio_server() as (read_stream, write_stream):
            await self.server.run(
                read_stream,
                write_stream,
                InitializationOptions(
                    server_name="media-clipping-mcp",
                    server_version="1.0.0",
                    capabilities=self.server.get_capabilities(
                        notification_options=NotificationOptions(),
                        experimental_capabilities={},
                    ),
                ),
            )


def main():
    """Entry point for the MCP server."""
    server = MediaClippingMCPServer()
    asyncio.run(server.run())


if __name__ == "__main__":
    main()