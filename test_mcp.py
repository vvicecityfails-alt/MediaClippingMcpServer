#!/usr/bin/env python3
"""Simple MCP client to test the MediaClippingMcpServer"""

import asyncio
import json
import subprocess
import sys

async def test_mcp_server():
    # Start the MCP server
    proc = await asyncio.create_subprocess_exec(
        'dotnet', 'run', '--project',
        'C:/Users/Administrator/source/repos/WebApplication1/MediaClippingMcpServer/MediaClippingMcpServer.csproj',
        stdin=asyncio.subprocess.PIPE,
        stdout=asyncio.subprocess.PIPE,
        stderr=asyncio.subprocess.PIPE,
    )

    async def send_request(request):
        """Send a JSON-RPC request and get response"""
        request_json = json.dumps(request) + '\n'
        proc.stdin.write(request_json.encode())
        await proc.stdin.drain()

        # Read response
        line = await proc.stdout.readline()
        if not line:
            return None
        return json.loads(line.decode().strip())

    try:
        # Initialize
        print("=== Initialize ===")
        response = await send_request({
            "jsonrpc": "2.0",
            "id": 1,
            "method": "initialize",
            "params": {
                "protocolVersion": "2024-11-05",
                "capabilities": {},
                "clientInfo": {"name": "test-client", "version": "1.0.0"}
            }
        })
        print(json.dumps(response, indent=2))

        # List tools
        print("\n=== List Tools ===")
        response = await send_request({
            "jsonrpc": "2.0",
            "id": 2,
            "method": "tools/list",
            "params": {}
        })
        print(json.dumps(response, indent=2))

        # Test get_media_info with a non-existent file (should fail gracefully)
        print("\n=== Test get_media_info (missing file) ===")
        response = await send_request({
            "jsonrpc": "2.0",
            "id": 3,
            "method": "tools/call",
            "params": {
                "name": "get_media_info",
                "arguments": {"inputPath": "C:/nonexistent.mp4"}
            }
        })
        print(json.dumps(response, indent=2))

    finally:
        proc.terminate()
        await proc.wait()

if __name__ == "__main__":
    asyncio.run(test_mcp_server())