#!/usr/bin/env python3
"""
MediaClippingMcpServer License Key Generator (Python)
Generates RSA-signed license keys for Pro/Enterprise tiers.
"""

import argparse
import base64
import json
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Optional

from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import padding, rsa
from cryptography.hazmat.primitives.asymmetric.rsa import RSAPrivateKey, RSAPublicKey


TIER_NAMES = {0: "Free", 1: "Pro", 2: "Enterprise"}


class LicensePayload:
    def __init__(self, tier: int, customer_email: str, expires_at: Optional[datetime], issued_at: datetime):
        self.tier = tier
        self.customer_email = customer_email
        self.expires_at = expires_at.isoformat() if expires_at else None
        self.issued_at = issued_at.isoformat()

    def to_dict(self):
        return {
            "Tier": self.tier,
            "CustomerEmail": self.customer_email,
            "ExpiresAt": self.expires_at,
            "IssuedAt": self.issued_at
        }

    def to_json(self) -> str:
        return json.dumps(self.to_dict(), separators=(',', ':'))


def generate_keypair(output_dir: Path) -> tuple[Path, Path]:
    """Generate RSA 2048-bit key pair and save as PEM files."""
    private_key: RSAPrivateKey = rsa.generate_private_key(
        public_exponent=65537,
        key_size=2048
    )
    public_key: RSAPublicKey = private_key.public_key()

    private_pem = private_key.private_bytes(
        encoding=serialization.Encoding.PEM,
        format=serialization.PrivateFormat.PKCS8,
        encryption_algorithm=serialization.NoEncryption()
    )
    public_pem = public_key.public_bytes(
        encoding=serialization.Encoding.PEM,
        format=serialization.PublicFormat.SubjectPublicKeyInfo
    )

    private_path = output_dir / "license_private_key.pem"
    public_path = output_dir / "license_public_key.pem"

    private_path.write_bytes(private_pem)
    public_path.write_bytes(public_pem)

    print(f"Generated key pair:")
    print(f"  Private key: {private_path}")
    print(f"  Public key:  {public_path}")
    print()
    print("IMPORTANT: Keep the private key secure! Add the public key to your appsettings.json:")
    print(f'  "LicensePublicKey": """{public_pem.decode().strip()}"""')

    return private_path, public_path


def load_private_key(key_path: Path) -> RSAPrivateKey:
    """Load RSA private key from PEM file."""
    pem_data = key_path.read_bytes()
    return serialization.load_pem_private_key(pem_data, password=None)


def load_public_key(key_path: Path) -> RSAPublicKey:
    """Load RSA public key from PEM file."""
    pem_data = key_path.read_bytes()
    return serialization.load_pem_public_key(pem_data)


def generate_license_key(
    private_key: RSAPrivateKey,
    tier: int,
    customer_email: str,
    expires_at: Optional[datetime]
) -> str:
    """Generate a signed license key."""
    issued_at = datetime.now(timezone.utc)
    payload = LicensePayload(tier, customer_email, expires_at, issued_at)
    payload_json = payload.to_json()
    payload_bytes = payload_json.encode('utf-8')

    # Sign the payload
    signature = private_key.sign(
        payload_bytes,
        padding.PKCS1v15(),
        hashes.SHA256()
    )

    # Encode as base64: payload.signature
    payload_b64 = base64.urlsafe_b64encode(payload_bytes).decode('ascii').rstrip('=')
    signature_b64 = base64.urlsafe_b64encode(signature).decode('ascii').rstrip('=')

    return f"{payload_b64}.{signature_b64}"


def verify_license_key(public_key: RSAPublicKey, license_key: str) -> Optional[dict]:
    """Verify a license key and return decoded payload if valid."""
    try:
        payload_b64, signature_b64 = license_key.split('.', 1)

        # Add padding back for base64 decode
        payload_bytes = base64.urlsafe_b64decode(payload_b64 + '==')
        signature = base64.urlsafe_b64decode(signature_b64 + '==')

        # Verify signature
        public_key.verify(
            signature,
            payload_bytes,
            padding.PKCS1v15(),
            hashes.SHA256()
        )

        # Decode payload
        payload = json.loads(payload_bytes.decode('utf-8'))

        # Check expiration
        if payload.get('ExpiresAt'):
            expires = datetime.fromisoformat(payload['ExpiresAt'].replace('Z', '+00:00'))
            if datetime.now(timezone.utc) > expires:
                print("License expired")
                return None

        return payload
    except Exception as e:
        print(f"Verification failed: {e}")
        return None


def main():
    parser = argparse.ArgumentParser(
        description="MediaClippingMcpServer License Key Generator",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  # Generate key pair
  python -m media_clipping_mcp.license_gen --generate-keypair

  # Generate Pro license key (expires in 1 year)
  python -m media_clipping_mcp.license_gen --private-key license_private_key.pem --tier 1 --email user@example.com

  # Generate Enterprise license with custom expiry
  python -m media_clipping_mcp.license_gen --private-key license_private_key.pem --tier 2 --email user@example.com --expires 2026-12-31

  # Verify a license key
  python -m media_clipping_mcp.license_gen --verify --public-key license_public_key.pem --license-key "eyJUaWVyIjoxLCJ..."
        """
    )

    parser.add_argument('--private-key', type=Path, help='Path to RSA private key PEM file')
    parser.add_argument('--public-key', type=Path, help='Path to RSA public key PEM file')
    parser.add_argument('--tier', type=int, choices=[0, 1, 2], help='License tier: 0=Free, 1=Pro, 2=Enterprise')
    parser.add_argument('--email', type=str, help='Customer email address')
    parser.add_argument('--expires', type=str, help='Expiration date (YYYY-MM-DD), default: 1 year')
    parser.add_argument('--output', type=Path, help='Output file path (default: stdout)')
    parser.add_argument('--generate-keypair', action='store_true', help='Generate new RSA key pair and exit')
    parser.add_argument('--keypair-output', type=Path, default=Path('.'), help='Directory to save generated key pair')
    parser.add_argument('--verify', action='store_true', help='Verify a license key')
    parser.add_argument('--license-key', type=str, help='License key to verify')

    args = parser.parse_args()

    if args.generate_keypair:
        generate_keypair(args.keypair_output)
        return 0

    if args.verify:
        if not args.public_key or not args.public_key.exists():
            print("Error: Public key file not found")
            return 1
        if not args.license_key:
            print("Error: --license-key required for verification")
            return 1

        public_key = load_public_key(args.public_key)
        payload = verify_license_key(public_key, args.license_key)
        if payload:
            print("License valid!")
            print(f"  Tier: {TIER_NAMES.get(payload['Tier'], 'Unknown')}")
            print(f"  Email: {payload['CustomerEmail']}")
            print(f"  Issued: {payload['IssuedAt']}")
            print(f"  Expires: {payload['ExpiresAt'] or 'Never'}")
        return 0

    # Generate license key
    if not args.private_key or not args.private_key.exists():
        print("Error: Private key file not found (use --generate-keypair to create)")
        return 1

    if args.tier is None:
        print("Error: --tier required (0=Free, 1=Pro, 2=Enterprise)")
        return 1

    if not args.email:
        print("Error: --email required")
        return 1

    expires_at = None
    if args.expires:
        try:
            expires_at = datetime.strptime(args.expires, '%Y-%m-%d').replace(tzinfo=timezone.utc)
        except ValueError:
            print("Error: --expires must be YYYY-MM-DD format")
            return 1
    else:
        expires_at = datetime.now(timezone.utc).replace(year=datetime.now().year + 1)

    private_key = load_private_key(args.private_key)
    license_key = generate_license_key(private_key, args.tier, args.email, expires_at)

    if args.output:
        args.output.write_text(license_key)
        print(f"License key written to {args.output}")
    else:
        print(license_key)

    return 0


if __name__ == '__main__':
    sys.exit(main())