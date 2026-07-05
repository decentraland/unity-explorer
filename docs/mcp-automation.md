# MCP Automation Server

The Explorer can host an embedded [MCP (Model Context Protocol)](https://modelcontextprotocol.io/) server so coding agents (e.g. Claude Code) can **see** the running client (screenshots, player/scene state, scene console logs) and **control** it (teleport, move, walk, look, chat commands, scene reload) — closing the edit → reload → verify loop for SDK7 scene development without a human in the middle.

The server is compiled into all builds but stays dormant unless explicitly enabled at launch.

---

## Enabling

| Flag | Effect |
|---|---|
| `--mcp` | Starts the MCP server on the default port **8123** |
| `--mcp-port <port>` | Starts the MCP server on a specific port (implies `--mcp`) |

The flag is accepted from the command line or a deep link. The endpoint is `http://127.0.0.1:<port>/mcp`.

```bash
# macOS
open Decentraland.app --args --mcp

# Windows
Decentraland.exe --mcp-port 8124
```

In the Unity Editor, add `--mcp` to `Main Scene Loader → Debug Settings → App Parameters`.

## Security model

- The listener binds to **127.0.0.1 only** — it is never reachable from the network.
- Browser-originated requests are rejected unless their `Origin` is localhost (defense against drive-by pages and DNS rebinding). Requests without an `Origin` header (CLI clients) are allowed.
- The server only exists while the process runs with the flag; there is no persistence and no authentication token in v1.

## Connecting a coding agent

```bash
claude mcp add --transport http explorer http://127.0.0.1:8123/mcp
```

Smoke test without an agent:

```bash
curl -s -X POST http://127.0.0.1:8123/mcp \
  -H 'Content-Type: application/json' \
  -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"curl","version":"0"}}}'

curl -s -X POST http://127.0.0.1:8123/mcp \
  -H 'Content-Type: application/json' \
  -d '{"jsonrpc":"2.0","id":2,"method":"tools/list"}'
```

## Tool catalog

### Seeing

| Tool | Arguments | Returns |
|---|---|---|
| `screenshot` | `maxWidth?` (default 1280), `quality?` (`jpg`\|`png`), `worldOnly?` | Downscaled image of the current view (UI included by default) + caption |
| `get_player_state` | — | Player position/rotation/parcel/velocity/grounded + camera position/rotation/mode |
| `get_scene_state` | — | Current parcel, scene name/state (incl. `JavaScriptError`/`EcsError`), readiness, loading stage |
| `get_scene_logs` | `limit?`, `severity?` (`all`\|`error`), `sinceSeq?` | Scene JS console output with monotonic sequence numbers for incremental polling |
| `list_scene_entities` | `limit?` | Entity ids of the current scene's ECS world |
| `get_entity_details` | `entityId` | All components of one scene entity |

### Controlling

| Tool | Arguments | Effect |
|---|---|---|
| `teleport` | `x`, `y`, `waitForReady?`, `timeoutSec?` | `/goto x,y` through the regular pipeline, waits for scene readiness |
| `move_to` | `x`, `y`, `z`, `lookAt{X,Y,Z}?`, `durationSec?` | Instant or smooth move to a world position (16 m per parcel) |
| `walk` | `directionX`, `directionY`, `seconds?`, `kind?`, `jump?` | Holds camera-relative movement through the real locomotion pipeline (collisions apply) |
| `look_at` | `x`, `y`, `z` | Rotates the camera to a world point (aim before a screenshot) |
| `set_camera_mode` | `mode` (`first_person`\|`third_person`\|`drone`\|`free`) | Switches the camera mode like the user hotkey; refuses (with the reason) while a scene locks the camera — `CameraModeArea`, scene virtual camera, or photo camera. `get_player_state` → `camera.modeChangeAllowed` reports the lock state in advance |
| `send_chat` | `message` | Sends to Nearby chat; `/commands` run through the chat command pipeline |
| `reload_scene` | `timeoutSec?` | Reloads the current scene (motion + skybox frozen during reload) |
| `trigger_emote` | `urn` or `stop: true`, `loop?` | Plays or stops an avatar emote |

## The scene-iteration loop

1. Serve the scene locally: `npm run start` in the scene folder (serves at `http://127.0.0.1:8000` and hot-reloads on file changes).
2. Launch the Explorer against it with the MCP server on:

```bash
open Decentraland.app --args \
  --realm http://127.0.0.1:8000 --local-scene true --position 0,0 \
  --debug --skip-auth-screen --skip-version-check true \
  --mcp --windowed-mode --resolution 1280x720
```

Optional determinism flags for stable screenshots: `--disable-hud`, `--skybox-time-enabled false`, `--landscape-terrain-enabled false`, `--skip-minimum-specs-screen`.

3. The agent then loops: edit scene TypeScript → LSD hot reload applies it (or call `reload_scene`) → `get_scene_state` until ready → `screenshot` + `get_scene_logs` → verify → repeat.

A user-invokable Claude Code skill wrapping this loop lives at `.claude/skills/mcp-scene-iteration/` (invoke with `/mcp-scene-iteration`).

## Troubleshooting

- **Port already in use** — the server logs an `MCP` category error and stays inert; relaunch with a different `--mcp-port`. Multiple Explorer instances (`--multi-instance`) each need their own port.
- **HTTP 403** — the request carried a non-localhost `Origin` header; MCP clients and curl don't send one.
- **Server won't start on Windows** — `HttpListener` may require a URL ACL depending on machine policy: `netsh http add urlacl url=http://127.0.0.1:8123/mcp/ user=Everyone` (elevated prompt), then relaunch.
- **Verbose logs** — enabling the server registers a scene-console log handler, which turns on unconditional verbose logging for the session (same behavior as `--scene-console`).
- **Scene entity dumps** — `list_scene_entities`/`get_entity_details` read the scene world without acquiring its sync lock (same as the existing `WorldInfoTool` debug tooling); treat results as a diagnostic snapshot.

## Implementation map

- `Explorer/Assets/DCL/Mcp/` — feature folder (folded into `DCL.Plugins` via `.asmref`): `Protocol/` (JSON-RPC dispatcher), `Transport/` (`HttpListener` server + Origin validation), `Tools/` (one class per tool), `Systems/` (`McpInputOverrideSystem` for held movement), `McpServerPlugin.cs`.
- Registration: `DynamicWorldContainer.CreateAsync`, gated on `McpServerPlugin.IsEnabled(appArgs)`.
- Flags: `AppArgsFlags.MCP` / `AppArgsFlags.MCP_PORT`; log category: `ReportCategory.MCP`.
