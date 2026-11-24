"""Configuration management for MediaClippingMcpServer."""

import os
from pathlib import Path
from typing import Optional

from pydantic import Field
from pydantic_settings import BaseSettings, SettingsConfigDict


class LicenseServiceOptions(BaseSettings):
    """License service configuration."""

    model_config = SettingsConfigDict(
        env_prefix="LICENSE_",
        env_file=".env",
        env_file_encoding="utf-8",
        extra="ignore",
    )

    # Public key for verifying license signatures (PEM format)
    public_key: str = Field(
        default="",
        description="RSA public key in PEM format for license verification",
    )

    # Stripe secret key for subscription validation
    stripe_secret_key: str = Field(
        default="",
        description="Stripe secret key for validating subscriptions",
    )

    # Stripe webhook secret for verifying webhook signatures
    stripe_webhook_secret: str = Field(
        default="",
        description="Stripe webhook signing secret",
    )

    # License file path (alternative to env var)
    license_file_path: str = Field(
        default="license.json",
        description="Path to license file",
    )

    # Cache TTL for license validation (seconds)
    cache_ttl_seconds: int = Field(
        default=3600,
        description="License validation cache TTL in seconds",
    )


class ServerOptions(BaseSettings):
    """Main server configuration."""

    model_config = SettingsConfigDict(
        env_prefix="MEDIA_CLIP_",
        env_file=".env",
        env_file_encoding="utf-8",
        extra="ignore",
    )

    # FFmpeg path (auto-detect if not set)
    ffmpeg_path: Optional[str] = Field(
        default=None,
        description="Path to ffmpeg executable",
    )

    # FFprobe path (auto-detect if not set)
    ffprobe_path: Optional[str] = Field(
        default=None,
        description="Path to ffprobe executable",
    )

    # yt-dlp path (auto-detect if not set)
    ytdlp_path: Optional[str] = Field(
        default=None,
        description="Path to yt-dlp executable",
    )

    # Default output directory
    output_dir: str = Field(
        default="./output",
        description="Default output directory for processed files",
    )

    # Max concurrent operations
    max_concurrent: int = Field(
        default=4,
        description="Maximum concurrent FFmpeg operations",
    )

    # Log level
    log_level: str = Field(
        default="INFO",
        description="Logging level",
    )

    # License configuration
    license: LicenseServiceOptions = Field(default_factory=LicenseServiceOptions)


def get_config() -> ServerOptions:
    """Get server configuration from environment and config files."""
    return ServerOptions()


def get_license_config() -> LicenseServiceOptions:
    """Get license configuration."""
    return LicenseServiceOptions()