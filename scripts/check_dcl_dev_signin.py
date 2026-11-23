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

import launch_dcl_dev as launch


def rpc(method, params, session=None, notify=False):
    uri = "http://127.0.0.1:8123/unity-explorer-mcp"
    headers = {"Content-Type": "application/json", "Accept": "application/json, text/event-stream",
               "MCP-Protocol-Version": "2025-06-18"}
    body = {"jsonrpc": "2.0", "method": method, "params": params}
    if not notify:
        body["id"] = 1
    if session:
        headers["Mcp-Session-Id"] = session
    with urlopen(Request(uri, headers=headers, data=json.dumps(body).encode()), timeout=12) as resp:
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


def wait_for_signin(process, log):
    url_file = Path(os.environ["USERPROFILE"]) / "AppData/LocalLow/Decentraland/Explorer/auth-url.txt"
    session = None
    clicked = set()
    last_paths = []
    end = time.monotonic() + 300
    while time.monotonic() < end and process.poll() is None:
        if url_file.exists():
            url = urlsplit(url_file.read_text(encoding="utf-8-sig"))
            query = parse_qs(url.query, keep_blank_values=True)
            if (url.scheme, url.netloc) != ("https", "dcl.dev") or not re.fullmatch(
                    r"/auth/requests/[0-9a-f-]{36}", url.path):
                raise ValueError("Player opened an unexpected wallet login origin or path")
            if query.get("flow") != ["deeplink"] or "bridgeOnly" not in query:
                raise ValueError("Missing wallet callback arguments")
            print("Verified the running CI player generated https://dcl.dev/auth/requests/<id> with the bridge callback.")
            return
        if not session:
            try:
                _, session = rpc("initialize", {"protocolVersion": "2025-06-18", "capabilities": {},
                                                "clientInfo": {"name": "signin-smoke", "version": "1"}})
            except OSError:
                time.sleep(3)
                continue
            rpc("notifications/initialized", {}, session, notify=True)
        elements = tool(session, "ui_list", {"stack": "ugui"})["elements"]
        last_paths = [e.get("path", "") for e in elements]
        for token in ["Metamask", "WalletConnect", "LogInOrSignup", "MoreOptions"]:
            match = next((e for e in elements if token.lower() in e["path"].lower()
                          and e.get("kind") == "button" and token not in clicked), None)
            if match:
                tool(session, "ui_click", {"path": match["path"]})
                print(f"Selected {token}", flush=True)
                clicked.add(token)
                break
        time.sleep(3)
    print("Last UI controls:", [path.rsplit("/", 1)[-1] for path in last_paths][:25])
    content = log.read_text(encoding="utf-8", errors="replace") if log.exists() else ""
    print("Exceptions:", sorted(set(re.findall(r"^([\w.]+Exception):", content, re.M))))
    print("Player exit:", process.poll(), "log size:", len(content))
    print("Startup markers:", {key: key in content for key in
          ("GfxDevice", "Failed to initialize graphics", "NullGfxDevice", "Enabled feature flags", "MCP server listening", "Cannot resolve")})
    raise RuntimeError("Player did not reach wallet sign-in")


with tempfile.TemporaryDirectory(dir=os.environ["RUNNER_TEMP"]) as tmp:
    folder = Path(tmp)
    exe = launch.extract_player(folder, None)
    log = folder / "Player.log"
    with launch.session_flags() as flags:
        launch.check_deployment(flags)
        args = [exe, "-force-d3d11", "-force-driver-type-warp", "-screen-fullscreen", "0",
                "-logFile", log, *launch.player_arguments(flags), "--skip-minimum-specs-screen",
                "--mcp-port", "8123", "--alttester"]
        p = subprocess.Popen(args, cwd=folder)
        try:
            wait_for_signin(p, log)
        finally:
            if p.poll() is None:
                p.terminate()
            p.wait(timeout=30)
