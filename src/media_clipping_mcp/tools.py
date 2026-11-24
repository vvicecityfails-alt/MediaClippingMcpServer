"""Media clipping tools for MCP server."""

import asyncio
import json
import logging
import shutil
import subprocess
import tempfile
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Optional

import ffmpeg
import yt_dlp

from .config import ServerOptions
from .license import LicenseService, LicenseTier

logger = logging.getLogger(__name__)


@dataclass
class MediaInfo:
    """Media file metadata."""
    duration: float
    width: Optional[int]
    height: Optional[int]
    video_codec: Optional[str]
    audio_codec: Optional[str]
    bitrate: Optional[int]
    format_name: str
    file_size: int


class MediaClippingService:
    """Core media processing service using FFmpeg."""

    def __init__(self, options: ServerOptions):
        self.options = options
        self._ffmpeg_path = options.ffmpeg_path or shutil.which("ffmpeg")
        self._ffprobe_path = options.ffprobe_path or shutil.which("ffprobe")
        self._ytdlp_path = options.ytdlp_path or shutil.which("yt-dlp")

        if not self._ffmpeg_path:
            raise RuntimeError("FFmpeg not found. Install FFmpeg and add to PATH.")
        if not self._ffprobe_path:
            raise RuntimeError("FFprobe not found. Install FFmpeg and add to PATH.")

        # Configure ffmpeg-python
        ffmpeg._run.FFMPEG_BINARY = self._ffmpeg_path
        ffmpeg._run.FFPROBE_BINARY = self._ffprobe_path

    def get_media_info(self, input_path: str) -> MediaInfo:
        """Get media file metadata using ffprobe."""
        path = Path(input_path)
        if not path.exists():
            raise FileNotFoundError(f"Input file not found: {input_path}")

        probe = ffmpeg.probe(input_path)

        # Find video and audio streams
        video_stream = next((s for s in probe["streams"] if s["codec_type"] == "video"), None)
        audio_stream = next((s for s in probe["streams"] if s["codec_type"] == "audio"), None)

        format_info = probe.get("format", {})

        return MediaInfo(
            duration=float(format_info.get("duration", 0)),
            width=video_stream.get("width") if video_stream else None,
            height=video_stream.get("height") if video_stream else None,
            video_codec=video_stream.get("codec_name") if video_stream else None,
            audio_codec=audio_stream.get("codec_name") if audio_stream else None,
            bitrate=int(format_info.get("bit_rate", 0)) if format_info.get("bit_rate") else None,
            format_name=format_info.get("format_name", ""),
            file_size=path.stat().st_size,
        )

    def _parse_time(self, time_str: str) -> float:
        """Parse time string (HH:MM:SS or seconds) to seconds."""
        if ":" in time_str:
            parts = time_str.split(":")
            if len(parts) == 3:
                return int(parts[0]) * 3600 + int(parts[1]) * 60 + float(parts[2])
            elif len(parts) == 2:
                return int(parts[0]) * 60 + float(parts[1])
        return float(time_str)

    def clip_media(
        self,
        input_path: str,
        output_path: str,
        start_time: str,
        end_time: Optional[str] = None,
        duration: Optional[str] = None,
    ) -> None:
        """Trim media by timeframe."""
        start = self._parse_time(start_time)

        stream = ffmpeg.input(input_path, ss=start)

        if end_time:
            end = self._parse_time(end_time)
            duration_sec = end - start
            stream = stream.output(output_path, t=duration_sec, c="copy")
        elif duration:
            duration_sec = self._parse_time(duration)
            stream = stream.output(output_path, t=duration_sec, c="copy")
        else:
            stream = stream.output(output_path, c="copy")

        ffmpeg.run(stream, overwrite_output=True, capture_stdout=True, capture_stderr=True)

    def extract_audio(
        self,
        input_path: str,
        output_path: str,
        audio_codec: str = "libmp3lame",
        bitrate: str = "192k",
    ) -> None:
        """Extract audio from video."""
        stream = ffmpeg.input(input_path)
        stream = stream.output(
            output_path,
            acodec=audio_codec,
            audio_bitrate=bitrate,
            vn=None,  # No video
        )
        ffmpeg.run(stream, overwrite_output=True, capture_stdout=True, capture_stderr=True)

    def convert_media(
        self,
        input_path: str,
        output_path: str,
        video_codec: Optional[str] = None,
        audio_codec: Optional[str] = None,
        preset: Optional[str] = None,
    ) -> None:
        """Convert media format."""
        stream = ffmpeg.input(input_path)

        output_args = {}
        if video_codec:
            output_args["vcodec"] = video_codec
        if audio_codec:
            output_args["acodec"] = audio_codec
        if preset:
            output_args["preset"] = preset

        stream = stream.output(output_path, **output_args)
        ffmpeg.run(stream, overwrite_output=True, capture_stdout=True, capture_stderr=True)

    async def download_media(
        self,
        url: str,
        output_path: str,
        format: Optional[str] = None,
        audio_only: bool = False,
    ) -> None:
        """Download media from URL using yt-dlp."""
        if not self._ytdlp_path:
            raise RuntimeError("yt-dlp not found. Install yt-dlp for URL downloads.")

        # Configure yt-dlp options
        ydl_opts = {
            "outtmpl": output_path,
            "quiet": True,
            "no_warnings": True,
        }

        if audio_only:
            ydl_opts.update({
                "format": "bestaudio/best",
                "postprocessors": [{
                    "key": "FFmpegExtractAudio",
                    "preferredcodec": format or "mp3",
                    "preferredquality": "192",
                }],
            })
        elif format:
            ydl_opts["format"] = f"bestvideo[ext={format}]+bestaudio[ext=m4a]/best[ext={format}]/best"

        # Run yt-dlp in thread pool to avoid blocking
        loop = asyncio.get_event_loop()
        await loop.run_in_executor(None, self._run_ytdlp, url, ydl_opts)

    def _run_ytdlp(self, url: str, ydl_opts: dict) -> None:
        """Run yt-dlp synchronously."""
        with yt_dlp.YoutubeDL(ydl_opts) as ydl:
            ydl.download([url])


class MediaTools:
    """MCP tool implementations with license gating."""

    def __init__(self, service: MediaClippingService, license_service: LicenseService):
        self.service = service
        self.license = license_service

    def get_media_info(self, input_path: str) -> dict:
        """Get media metadata - Free tier."""
        info = self.service.get_media_info(input_path)
        return {
            "duration": info.duration,
            "width": info.width,
            "height": info.height,
            "video_codec": info.video_codec,
            "audio_codec": info.audio_codec,
            "bitrate": info.bitrate,
            "format": info.format_name,
            "file_size": info.file_size,
        }

    def clip_media(
        self,
        input_path: str,
        output_path: str,
        start_time: str,
        end_time: Optional[str] = None,
        duration: Optional[str] = None,
    ) -> dict:
        """Trim media - Free tier."""
        self.license.require_feature("local_ops")
        self.service.clip_media(input_path, output_path, start_time, end_time, duration)
        return {"success": True, "output_path": output_path}

    def extract_audio(
        self,
        input_path: str,
        output_path: str,
        audio_codec: str = "libmp3lame",
        bitrate: str = "192k",
    ) -> dict:
        """Extract audio - Free tier."""
        self.license.require_feature("local_ops")
        self.service.extract_audio(input_path, output_path, audio_codec, bitrate)
        return {"success": True, "output_path": output_path}

    def convert_media(
        self,
        input_path: str,
        output_path: str,
        video_codec: Optional[str] = None,
        audio_codec: Optional[str] = None,
        preset: Optional[str] = None,
    ) -> dict:
        """Convert media format - Free tier."""
        self.license.require_feature("local_ops")
        self.service.convert_media(input_path, output_path, video_codec, audio_codec, preset)
        return {"success": True, "output_path": output_path}

    async def download_media(
        self,
        url: str,
        output_path: str,
        format: Optional[str] = None,
        audio_only: bool = False,
    ) -> dict:
        """Download from URL - Pro tier required."""
        self.license.require_feature("download")
        await self.service.download_media(url, output_path, format, audio_only)
        return {"success": True, "output_path": output_path}