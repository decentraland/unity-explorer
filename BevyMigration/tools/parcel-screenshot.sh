#!/usr/bin/env bash
# One world-only screenshot of a parcel through a driven Editor play session, for the step 3 gate
# (BevyMigration/docs/README.md § 7, G-A3): run it once with `unity` and once with `bevy`, then compare
# the two files with `cargo run -p screenshot-diff`.
#
# Usage: parcel-screenshot.sh <unity|bevy> <out.png> [--parcel X,Y] [--camera "x y z lookX lookY lookZ"]
#                             [--seconds N] [--extra "<more app parameters>"] [--look-player]
# --look-player aims the camera at the player's avatar instead of the fixed pose (the step 4 gate).
#
# Needs: a windowed Editor (batch mode cannot build URP's renderer), a persisted identity in the Editor
# (the session lands straight on the parcel), and the MCP server, which `--mcp` turns on.
set -euo pipefail

world="${1:?world: unity or bevy}"
out="${2:?output png}"
shift 2
parcel="0,0"
camera="8 6 -12 8 2 8"
seconds=240
extra=""
look_player=0
second=""

while [ $# -gt 0 ]; do
  case "$1" in
    --parcel) parcel="$2"; shift 2 ;;
    --camera) camera="$2"; shift 2 ;;
    --seconds) seconds="$2"; shift 2 ;;
    --extra) extra="$2"; shift 2 ;;
    --look-player) look_player=1; shift ;;
    --then-player) second="$2"; shift 2 ;;
    *) echo "unknown argument: $1" >&2; exit 2 ;;
  esac
done

repo="$(cd "$(dirname "$0")/../.." && pwd)"
unity="${UNITY:-/Applications/Unity/Hub/Editor/6000.5.9f1/Unity.app/Contents/MacOS/Unity}"
mcp="${MCP_URL:-http://127.0.0.1:8123/unity-explorer-mcp}"
log="${out%.png}.log"
mkdir -p "$(dirname "$out")"

"$unity" -projectPath "$repo/Explorer" \
  -executeMethod DCL.BevyMigration.Editor.EditorPlaySession.RunBatch \
  -bmAppParams "--world $world --mcp --position $parcel --skip-version-check --skip-minimum-specs-screen $extra" \
  -bmSeconds "$seconds" -bmCycles 1 -accept-apiupdate -logFile "$log" &
unity_pid=$!
echo "[$world] Unity pid $unity_pid, log $log"

MCP_URL="$mcp" OUT="$out" CAMERA="$camera" WORLD="$world" LOG="$log" LOOK_PLAYER="$look_player" SECOND="$second" python3 -I - <<'PY'
import base64, json, os, sys, time, urllib.request, urllib.error

mcp, out, world = os.environ["MCP_URL"], os.environ["OUT"], os.environ["WORLD"]
camera = [float(v) for v in os.environ["CAMERA"].split()]

def rpc(method, params, timeout=60):
    body = json.dumps({"jsonrpc": "2.0", "id": 1, "method": method, "params": params}).encode()
    req = urllib.request.Request(mcp, data=body, headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=timeout) as r:
        return json.load(r)

def call(name, args, timeout=60):
    return rpc("tools/call", {"name": name, "arguments": args}, timeout)

def text(result):
    return next((c["text"] for c in result["result"]["content"] if c.get("type") == "text"), "")

deadline = time.time() + 300
while True:
    try:
        rpc("initialize", {"protocolVersion": "2025-06-18", "capabilities": {}, "clientInfo": {"name": "parcel-screenshot", "version": "0"}}, 3)
        break
    except (urllib.error.URLError, OSError, ValueError):
        try:
            with open(os.environ["LOG"], errors="replace") as log:
                if "Scripts have compiler errors" in log.read():
                    print(f"[{world}] Unity scripts have compiler errors, see {os.environ['LOG']}", file=sys.stderr); sys.exit(1)
        except OSError:
            pass
        if time.time() > deadline:
            print(f"[{world}] MCP server never came up", file=sys.stderr); sys.exit(1)
        time.sleep(1)
print(f"[{world}] MCP up")

deadline = time.time() + 240
while True:
    try:
        state = json.loads(text(call("get_scene_state", {}, 30)))
        stage = state.get("loadingStage")
        print(f"[{world}] scene: {stage} {(state.get('scene') or {}).get('name')}")
        if stage == "Completed":
            break
    except (urllib.error.URLError, OSError, ValueError, KeyError) as error:
        print(f"[{world}] scene state unavailable: {error}")
    if time.time() > deadline:
        print(f"[{world}] scene never completed", file=sys.stderr); sys.exit(1)
    time.sleep(10)

# Let the resource uploads and the periodic scene scan settle before posing the camera.
time.sleep(30)
print(f"[{world}] camera mode:", text(call("set_camera_mode", {"mode": "free"}))[:120])
x, y, z, lx, ly, lz = camera
if os.environ.get("LOOK_PLAYER") == "1":
    player = json.loads(text(call("get_player_state", {})))
    position = player.get("position") or {}
    px, py, pz = (float(position.get(k, 0.0)) for k in ("x", "y", "z"))
    x, y, z, lx, ly, lz = px, py + 1.4, pz - 3.0, px, py + 0.9, pz
    print(f"[{world}] player at {px:.2f} {py:.2f} {pz:.2f}")
print(f"[{world}] camera pose:", text(call("set_camera_pose", {"x": x, "y": y, "z": z, "lookAtX": lx, "lookAtY": ly, "lookAtZ": lz}))[:160].replace("\n", " "))
time.sleep(3)

shot = call("screenshot", {"maxWidth": 640, "quality": "png", "worldOnly": True}, 120)
image = next((c for c in shot["result"]["content"] if c.get("type") == "image"), None)
if image is None:
    print(f"[{world}] no image in the screenshot result: {json.dumps(shot)[:300]}", file=sys.stderr); sys.exit(1)
with open(out, "wb") as f:
    f.write(base64.b64decode(image["data"]))
print(f"[{world}] wrote {out}: {text(shot)}")

second = os.environ.get("SECOND")
if second:
    player = json.loads(text(call("get_player_state", {})))
    position = player.get("position") or {}
    px, py, pz = (float(position.get(k, 0.0)) for k in ("x", "y", "z"))
    print(f"[{world}] player at {px:.2f} {py:.2f} {pz:.2f}")
    print(f"[{world}] camera pose 2:", text(call("set_camera_pose", {"x": px, "y": py + 1.4, "z": pz - 3.0, "lookAtX": px, "lookAtY": py + 0.9, "lookAtZ": pz}))[:80].replace("\n", " "))
    time.sleep(3)
    shot = call("screenshot", {"maxWidth": 640, "quality": "png", "worldOnly": True}, 120)
    image = next((c for c in shot["result"]["content"] if c.get("type") == "image"), None)
    if image is not None:
        with open(second, "wb") as f:
            f.write(base64.b64decode(image["data"]))
        print(f"[{world}] wrote {second}: {text(shot)}")
PY

echo "[$world] waiting for the play session deadline ($seconds s) so the Editor exits cleanly"
wait "$unity_pid" || true
