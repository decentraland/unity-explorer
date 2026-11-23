"""Run the Windows player from PR #10686 against the dcl.dev gateway."""

import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
from contextlib import contextmanager
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from threading import Thread
from urllib.request import Request, urlopen
from zipfile import ZipFile


GATEWAY = "https://dcl.dev"
PULSE_HOST = "dcl-one-poc-rt.decentraland.zone"
BUILD_URL = (
    "https://explorer-artifacts.decentraland.org/@dcl/unity-explorer/branch/"
    "feat/realm-scene-pointers-pulse-server/pr-28106-ed38dde/Decentraland_windows64.zip"
)
ZIP_SHA256 = "273ab8a44546cb9d6119feb7cb72192a966c09d9ddac7419f22ebf24b2c5be2c"
REGULAR_ASSET_FLAGS = ("explorer-alfa-abgen-pipeline", "explorer-alfa-abgen-lods")


def fetch_json(url, body=None):
    request = Request(url, headers={"Referer": GATEWAY, "Content-Type": "application/json"},
                      data=None if body is None else json.dumps(body).encode())
    with urlopen(request, timeout=30) as response:
        return json.load(response)


@contextmanager
def session_flags():
    document = fetch_json(f"{GATEWAY}/feature-flags/explorer.json")
    for flag in REGULAR_ASSET_FLAGS:
        document["flags"][flag] = False
        if flag in document["variants"]:
            document["variants"][flag]["enabled"] = False
    payload = json.dumps(document).encode()

    class FlagsHandler(BaseHTTPRequestHandler):
        def do_GET(self):
            if self.path != "/explorer.json":
                self.send_error(404)
                return
            self.send_response(200)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(payload)))
            self.end_headers()
            self.wfile.write(payload)

        def log_message(self, *_):
            pass

    with ThreadingHTTPServer(("127.0.0.1", 0), FlagsHandler) as server:
        thread = Thread(target=server.serve_forever, daemon=True)
        thread.start()
        try:
            yield f"http://127.0.0.1:{server.server_port}"
        finally:
            server.shutdown()
            thread.join()


def extract_player(folder, archive):
    if archive is None:
        archive = folder / "windows.zip"
        subprocess.run(["curl.exe", "--fail", "--location", "--silent", "--show-error",
                        "--retry", "2", "--max-time", "600", "--output", str(archive), BUILD_URL],
                       check=True)
    with archive.open("rb") as stream:
        if hashlib.file_digest(stream, "sha256").hexdigest() != ZIP_SHA256:
            raise ValueError("ZIP digest differs from the published ed38dde Windows CI player")
    with ZipFile(archive) as bundle:
        bundle.extractall(folder)
    player = folder / "Decentraland.exe"
    if not player.is_file() or not (folder / "GameAssembly.dll").is_file():
        raise ValueError("CI archive is missing the Windows player or IL2CPP assembly")
    return player


def player_arguments(flags_url):
    return [
        "--base-domain", "dcl.dev", "--eth-network", "mainnet",
        "--gateway", GATEWAY, "--realm", GATEWAY,
        "--feature-flags-url", flags_url,
        "--gatekeeper-url", f"{GATEWAY}/comms-gatekeeper",
        "--friends-api-url", "wss://dcl.dev/social-rpc",
        "--pulse", "true", "--pulse-server", PULSE_HOST,
        "--realm-scene-pointers", "--position", "100,100",
        "--login-bridge-only", "--skip-version-check",
        "--debug", "--landscape-terrain-enabled", "false",
    ]


def check_deployment(flags_url):
    about = fetch_json(f"{GATEWAY}/about")
    if not about["acceptingUsers"] or about["configurations"]["networkId"] != 1:
        raise ValueError("The realm must accept users on mainnet")
    flags = fetch_json(f"{flags_url}/explorer.json")
    if not flags["flags"].get("explorer-alfa-minimum-requirements") or not all(
        flags["flags"].get(flag) is False for flag in REGULAR_ASSET_FLAGS
    ):
        raise ValueError("Missing startup requirements or incorrect asset pipeline")
    requirements = flags["variants"]["explorer-alfa-minimum-requirements"]
    if not json.loads(requirements["payload"]["value"])["windows_supported_versions"]:
        raise ValueError("The minimum-requirements payload is missing Windows versions")
    if not fetch_json(f"{GATEWAY}/auth-api/health").get("timestamp"):
        raise ValueError("The gateway auth API did not return its health timestamp")
    page = f"{GATEWAY}/auth/requests/00000000-0000-4000-8000-000000000000?loginMethod=Metamask&flow=deeplink&bridgeOnly"
    with urlopen(page, timeout=30) as response:
        if response.url != page or response.headers.get_content_type() != "text/html":
            raise ValueError("The wallet sign-in page did not resolve on dcl.dev")
    if not fetch_json(f"{GATEWAY}/comms-gatekeeper/status").get("version"):
        raise ValueError("The gateway gatekeeper did not return its status")
    scene, = fetch_json(f"{GATEWAY}/content/entities/active", {"pointers": ["100,100"]})
    fetch_json(f"{GATEWAY}/ab-cdn/manifest/{scene['id']}_windows.json")
    print(f"Realm {about['configurations']['realmName']}, island {about['comms']['adapter']}")
    print("Wallet sign-in page, auth API and scene gatekeeper available on dcl.dev")
    print(f"Scene {scene['id']} at 100,100, Windows manifest available")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--archive", type=Path, help="existing copy of the pinned CI ZIP")
    parser.add_argument("--work-dir", type=Path, default=Path(r"E:\Decentraland\.tmp\dcl-dev-explorer"))
    parser.add_argument("--check", action="store_true", help="verify the archive and deployment without opening Unity")
    args = parser.parse_args()
    root = args.work_dir.resolve()
    root.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="player-", dir=root) as temp:
        folder = Path(temp)
        player = extract_player(folder, args.archive)
        with session_flags() as flags_url:
            launch_args = player_arguments(flags_url)
            print("CI player ed38dde verified.", flush=True)
            print(subprocess.list2cmdline([str(player), *launch_args]), flush=True)
            if args.check:
                check_deployment(flags_url)
                return 0
            with tempfile.NamedTemporaryFile(prefix="Player-", suffix=".log", dir=root, delete=False) as log:
                log_path = log.name
            print(f"Player log: {log_path}", flush=True)
            env = dict(os.environ, TEMP=temp, TMP=temp)
            return subprocess.run([str(player), "-logFile", log_path, *launch_args],
                                  cwd=folder, env=env).returncode


if __name__ == "__main__":
    sys.exit(main())
