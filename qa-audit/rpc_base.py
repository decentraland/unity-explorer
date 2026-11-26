"""One-off hosted Windows check. The test runner starts with an empty profile."""
import json
import os
from pathlib import Path
import re
import subprocess
import tempfile
import time
from urllib.parse import parse_qs, urlsplit
from urllib.request import Request, urlopen




def rpc(method, params, session=None, notify=False):
    uri = f"http://127.0.0.1:{os.environ.get('AUDIT_MCP_PORT', '8123')}/unity-explorer-mcp"
    headers = {"Content-Type": "application/json", "Accept": "application/json, text/event-stream",
               "MCP-Protocol-Version": "2025-06-18"}
    body = {"jsonrpc": "2.0", "method": method, "params": params}
    if not notify:
        body["id"] = 1
    if session:
        headers["Mcp-Session-Id"] = session
    with urlopen(Request(uri, headers=headers, data=json.dumps(body).encode()), timeout=45) as resp:
        if notify:
            return
        payload = resp.read().decode().strip()
        if not payload.startswith("{"):
            payload = "\n".join(line[5:] for line in payload.splitlines() if line.startswith("data:"))
        result = json.loads(payload)
        if "error" in result:
            raise RuntimeError(result["error"]["code"])
        return result["result"], resp.headers.get("Mcp-Session-Id")


def tool(session, name, args):
    result, _ = rpc("tools/call", {"name": name, "arguments": args}, session)
    if result.get("isError"):
        raise RuntimeError(f"{name} returned an error")
    return json.loads(result["content"][0]["text"])


