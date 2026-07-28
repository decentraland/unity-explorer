#!/usr/bin/env bash
# Capture Explorer screenshots to disk via the embedded MCP server, without spending agent context.
# Frames are saved as files; only the caption (resolution + parcel) is printed. Read a frame file
# only when you actually need to inspect it.
#
# Usage: screenshot.sh [options]
#   -o, --out FILE        output file (single capture; extension follows quality)
#   -d, --dir DIR         output directory (default: mcp-shots/; used for bursts and when -o is omitted)
#   -n, --count N         number of frames to capture (default: 1)
#   -i, --interval SEC    seconds between frames in a burst (default: 0.5; keep >= 0.2, captures are serialized)
#   -w, --max-width PX    maxWidth passed to the tool (default: 1280; use 640 for cheap sanity checks)
#       --png             capture PNG instead of JPG
#       --world-only      exclude UI overlays (worldOnly: true)
#   -p, --port PORT       MCP server port (default: 8123)
#
# Requires: curl, python3. The Explorer must be running with --mcp.

set -euo pipefail

PORT=8123
MAX_WIDTH=1280
QUALITY=jpg
WORLD_ONLY=false
COUNT=1
INTERVAL=0.5
OUT=""
OUT_DIR="mcp-shots"

while [[ $# -gt 0 ]]; do
    case "$1" in
        -o|--out) OUT="$2"; shift 2 ;;
        -d|--dir) OUT_DIR="$2"; shift 2 ;;
        -n|--count) COUNT="$2"; shift 2 ;;
        -i|--interval) INTERVAL="$2"; shift 2 ;;
        -w|--max-width) MAX_WIDTH="$2"; shift 2 ;;
        --png) QUALITY=png; shift ;;
        --world-only) WORLD_ONLY=true; shift ;;
        -p|--port) PORT="$2"; shift 2 ;;
        -h|--help) sed -n '2,17p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
        *) echo "unknown argument: $1 (see --help)" >&2; exit 2 ;;
    esac
done

capture_one() {
    local target_file="$1"

    local payload
    payload=$(printf '{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"screenshot","arguments":{"maxWidth":%s,"quality":"%s","worldOnly":%s}}}' \
        "$MAX_WIDTH" "$QUALITY" "$WORLD_ONLY")

    # NOTE: response must land in a file, not a pipe into `python3 - <<heredoc`:
    # the heredoc IS python's stdin (the program), so piped data would be lost.
    local response_file
    response_file=$(mktemp)

    curl -sS --max-time 30 -X POST "http://127.0.0.1:${PORT}/unity-explorer-mcp" \
        -H 'Content-Type: application/json' \
        -H 'Accept: application/json, text/event-stream' \
        -o "$response_file" \
        -d "$payload"

    python3 - "$target_file" "$response_file" <<'PY'
import base64, json, sys

with open(sys.argv[2], encoding="utf-8") as f:
    raw = f.read()

# Tolerate SSE framing (data: lines) in case a proxy or future transport wraps the response.
try:
    response = json.loads(raw)
except json.JSONDecodeError:
    data_lines = [line[5:].strip() for line in raw.splitlines() if line.startswith("data:")]
    response = json.loads("".join(data_lines))

error = response.get("error")
if error:
    sys.exit(f"MCP error {error.get('code')}: {error.get('message')}")

result = response.get("result") or {}
image = None
caption = ""

for item in result.get("content", []):
    if item.get("type") == "image":
        image = item
    elif item.get("type") == "text":
        caption = item.get("text", "")

if result.get("isError"):
    sys.exit(f"screenshot tool error: {caption}")

if image is None:
    sys.exit("no image content in the response")

with open(sys.argv[1], "wb") as f:
    f.write(base64.b64decode(image["data"]))

print(f"{sys.argv[1]}  ({caption})")
PY
    local status=$?
    rm -f "$response_file"
    return $status
}

if [[ -n "$OUT" && "$COUNT" -eq 1 ]]; then
    mkdir -p "$(dirname "$OUT")" 2>/dev/null || true
    capture_one "$OUT"
    exit 0
fi

mkdir -p "$OUT_DIR"
stamp=$(date +%Y%m%d-%H%M%S)

for ((frame = 1; frame <= COUNT; frame++)); do
    capture_one "${OUT_DIR}/shot-${stamp}-$(printf '%03d' "$frame").${QUALITY}"

    if (( frame < COUNT )); then
        sleep "$INTERVAL"
    fi
done
