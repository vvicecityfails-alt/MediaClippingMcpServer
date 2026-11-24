"""License validation for MediaClippingMcpServer."""

import base64
import json
import logging
import time
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Optional

import httpx
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import padding, rsa
from cryptography.hazmat.primitives.asymmetric.rsa import RSAPublicKey

from .config import LicenseServiceOptions

logger = logging.getLogger(__name__)


class LicenseTier:
    FREE = 0
    PRO = 1
    ENTERPRISE = 2

    NAMES = {
        FREE: "Free",
        PRO: "Pro",
        ENTERPRISE: "Enterprise",
    }

    FEATURES = {
        FREE: {"local_ops": True, "download": False, "batch": False, "priority": False, "custom_tools": False},
        PRO: {"local_ops": True, "download": True, "batch": True, "priority": True, "custom_tools": False},
        ENTERPRISE: {"local_ops": True, "download": True, "batch": True, "priority": True, "custom_tools": True},
    }


@dataclass
class LicensePayload:
    tier: int
    customer_email: str
    expires_at: Optional[datetime]
    issued_at: datetime

    @classmethod
    def from_dict(cls, data: dict) -> "LicensePayload":
        expires_at = None
        if data.get("ExpiresAt"):
            expires_at = datetime.fromisoformat(data["ExpiresAt"].replace("Z", "+00:00"))
        return cls(
            tier=data["Tier"],
            customer_email=data["CustomerEmail"],
            expires_at=expires_at,
            issued_at=datetime.fromisoformat(data["IssuedAt"].replace("Z", "+00:00")),
        )

    def to_dict(self) -> dict:
        return {
            "Tier": self.tier,
            "CustomerEmail": self.customer_email,
            "ExpiresAt": self.expires_at.isoformat() if self.expires_at else None,
            "IssuedAt": self.issued_at.isoformat(),
        }

    @property
    def tier_name(self) -> str:
        return LicenseTier.NAMES.get(self.tier, "Unknown")

    @property
    def features(self) -> dict:
        return LicenseTier.FEATURES.get(self.tier, LicenseTier.FEATURES[LicenseTier.FREE])

    @property
    def is_expired(self) -> bool:
        if self.expires_at is None:
            return False
        return datetime.now(timezone.utc) > self.expires_at

    def has_feature(self, feature: str) -> bool:
        return self.features.get(feature, False)


class LicenseService:
    """Handles license validation and feature gating."""

    def __init__(self, options: LicenseServiceOptions):
        self.options = options
        self._public_key: Optional[RSAPublicKey] = None
        self._cached_license: Optional[LicensePayload] = None
        self._cache_expires: float = 0

    def _load_public_key(self) -> Optional[RSAPublicKey]:
        """Load RSA public key from config."""
        if self._public_key is not None:
            return self._public_key

        if not self.options.public_key:
            logger.warning("No public key configured - license validation disabled")
            return None

        try:
            pem_data = self.options.public_key.encode()
            self._public_key = serialization.load_pem_public_key(pem_data)
            return self._public_key
        except Exception as e:
            logger.error(f"Failed to load public key: {e}")
            return None

    def _load_license_from_file(self) -> Optional[str]:
        """Load license key from file."""
        license_path = Path(self.options.license_file_path)
        if license_path.exists():
            return license_path.read_text().strip()
        return None

    def _load_license_from_env(self) -> Optional[str]:
        """Load license key from environment variable."""
        return os.environ.get("MEDIA_CLIP_LICENSE_KEY")

    def _get_license_key(self) -> Optional[str]:
        """Get license key from file or environment."""
        # Priority: env var > file
        key = self._load_license_from_env()
        if key:
            return key
        return self._load_license_from_file()

    def _verify_signature(self, public_key: RSAPublicKey, license_key: str) -> Optional[LicensePayload]:
        """Verify license key signature and return payload."""
        try:
            payload_b64, signature_b64 = license_key.split(".", 1)

            # Add padding for base64 decode
            payload_bytes = base64.urlsafe_b64decode(payload_b64 + "==")
            signature = base64.urlsafe_b64decode(signature_b64 + "==")

            # Verify signature
            public_key.verify(
                signature,
                payload_bytes,
                padding.PKCS1v15(),
                hashes.SHA256(),
            )

            # Decode payload
            payload_data = json.loads(payload_bytes.decode("utf-8"))
            return LicensePayload.from_dict(payload_data)
        except Exception as e:
            logger.debug(f"License verification failed: {e}")
            return None

    async def _validate_with_stripe(self, payload: LicensePayload) -> bool:
        """Validate license against Stripe subscription."""
        if not self.options.stripe_secret_key:
            return True  # No Stripe config, skip validation

        try:
            async with httpx.AsyncClient() as client:
                # Check customer subscription status
                response = await client.get(
                    f"https://api.stripe.com/v1/customers/{payload.customer_email}/subscriptions",
                    auth=(self.options.stripe_secret_key, ""),
                    params={"status": "active", "limit": 1},
                )
                if response.status_code == 200:
                    data = response.json()
                    return len(data.get("data", [])) > 0
        except Exception as e:
            logger.warning(f"Stripe validation failed: {e}")

        return False

    def get_license(self) -> LicensePayload:
        """Get current license (with caching)."""
        # Return cached if valid
        if self._cached_license and time.time() < self._cache_expires:
            return self._cached_license

        # No public key = free tier
        public_key = self._load_public_key()
        if public_key is None:
            self._cached_license = LicensePayload(
                tier=LicenseTier.FREE,
                customer_email="",
                expires_at=None,
                issued_at=datetime.now(timezone.utc),
            )
            self._cache_expires = time.time() + self.options.cache_ttl_seconds
            return self._cached_license

        # Get license key
        license_key = self._get_license_key()
        if not license_key:
            self._cached_license = LicensePayload(
                tier=LicenseTier.FREE,
                customer_email="",
                expires_at=None,
                issued_at=datetime.now(timezone.utc),
            )
            self._cache_expires = time.time() + self.options.cache_ttl_seconds
            return self._cached_license

        # Verify signature
        payload = self._verify_signature(public_key, license_key)
        if payload is None or payload.is_expired:
            logger.warning("Invalid or expired license key - falling back to Free tier")
            self._cached_license = LicensePayload(
                tier=LicenseTier.FREE,
                customer_email="",
                expires_at=None,
                issued_at=datetime.now(timezone.utc),
            )
            self._cache_expires = time.time() + self.options.cache_ttl_seconds
            return self._cached_license

        # Validate with Stripe if configured
        if self.options.stripe_secret_key and payload.tier > LicenseTier.FREE:
            import asyncio
            try:
                is_valid = asyncio.run(self._validate_with_stripe(payload))
                if not is_valid:
                    logger.warning("Stripe validation failed - falling back to Free tier")
                    self._cached_license = LicensePayload(
                        tier=LicenseTier.FREE,
                        customer_email="",
                        expires_at=None,
                        issued_at=datetime.now(timezone.utc),
                    )
                    self._cache_expires = time.time() + self.options.cache_ttl_seconds
                    return self._cached_license
            except RuntimeError:
                # Already in event loop, skip async validation
                pass

        self._cached_license = payload
        self._cache_expires = time.time() + self.options.cache_ttl_seconds
        return payload

    def require_feature(self, feature: str) -> None:
        """Raise exception if current license doesn't have feature."""
        license = self.get_license()
        if not license.has_feature(feature):
            raise PermissionError(
                f"Feature '{feature}' requires {LicenseTier.NAMES.get(min(t for t, f in LicenseTier.FEATURES.items() if f.get(feature)), 'Pro')} tier or higher. "
                f"Current tier: {license.tier_name}"
            )

    def get_tier(self) -> int:
        """Get current license tier."""
        return self.get_license().tier


import os  # Moved here to avoid circular import