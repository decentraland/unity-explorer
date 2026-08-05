# MCP Automation Server

The Explorer can host an embedded [MCP (Model Context Protocol)](https://modelcontextprotocol.io/) server so coding agents (e.g. Claude Code) can **see** the running client (screenshots, player/scene state, scene console logs) and **control** it (teleport, move, walk, look, chat commands, scene reload) — closing the edit → reload → verify loop for SDK7 scene development without a human in the middle.

The server is compiled into all builds but stays dormant unless explicitly enabled at launch.

---

## Enabling

| Flag | Effect |
|---|---|
| `--mcp` | Starts the MCP server on the default port **8123** |
| `--mcp-port <port>` | Starts the MCP server on a specific port (implies `--mcp`) |

The flag is accepted from the command line, or from a deep link whose target realm is loopback (`decentraland://?realm=http://127.0.0.1:8000&mcp=true`) — a deep link pointing at a remote realm drops it, see [`DeepLinkAllowlist`](../Explorer/Assets/DCL/Infrastructure/Global/AppArgs/DeepLinkAllowlist.cs). The endpoint is `http://127.0.0.1:<port>/unity-explorer-mcp`.

```bash
# macOS
open Decentraland.app --args --mcp

# Windows
Decentraland.exe --mcp-port 8124
```

In the Unity Editor, add `--mcp` to `Main Scene Loader → Debug Settings → App Parameters`.

From a scene folder, `@dcl/sdk-commands` can enable it at launch: `npm run start -- --mcp` (optionally `--mcp-port <port>`) forwards both flags into the deep link that auto-launches the installed client. Any extra Explorer params can follow a second standalone `--` (`npm run start -- --mcp -- --windowed-mode --resolution 1280x720`; npm consumes the first `--`).

## Security model

- The listener binds to **127.0.0.1 only** — it is never reachable from the network.
- Browser-originated requests are rejected unless their `Origin` is localhost (defense against drive-by pages and DNS rebinding). Requests without an `Origin` header (CLI clients) are allowed.
- The server only exists while the process runs with the flag; there is no persistence and no authentication token in v1.
- A deep link can only turn it on when the link's `realm` is loopback (deep-link allowlist tier 2, SEC-019/020), so a link aimed at a production realm cannot start the server. That gate narrows the drive-by surface rather than closing it: a crafted link can supply a loopback realm of its own. Because there is no token, treat an open port as full local control of the client — screenshots, chat commands as the signed-in user, movement — and only enable it on a machine where every local process is trusted.

## Connecting a coding agent

```bash
claude mcp add --transport http --scope user explorer http://127.0.0.1:8123/unity-explorer-mcp
```

Smoke test without an agent:

```bash
curl -s -X POST http://127.0.0.1:8123/unity-explorer-mcp \
  -H 'Content-Type: application/json' \
  -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"curl","version":"0"}}}'

curl -s -X POST http://127.0.0.1:8123/unity-explorer-mcp \
  -H 'Content-Type: application/json' \
  -d '{"jsonrpc":"2.0","id":2,"method":"tools/list"}'
```

## Tool catalog

The tables below are a human-readable overview. The authoritative argument contract — exact types, allowed values, defaults — is what the server itself reports via `tools/list`; agents get it fresh at every handshake and should rely on it, not on this page.

### Seeing

| Tool | Arguments | Returns |
|---|---|---|
| `screenshot` | `maxWidth?` (default 1280), `quality?`, `worldOnly?` (exclude UI; post-processing still applied) | Downscaled image of the current view (UI included by default) + caption |
| `get_player_state` | — | Player position/rotation/parcel/velocity/grounded + camera position/rotation/mode + wallet address |
| `get_scene_state` | — | Current parcel, scene name/state (incl. `JavaScriptError`/`EcsError`), readiness, loading stage |
| `get_scene_content_stats` | — | Current scene's content stats (entities, triangles, bodies, geometries, materials, textures, shader variants, colliders, videos) with the documented soft-limit caps for its parcel count (materials is shown uncapped — see the SRP Batcher note below); triggers a fresh counting pass |
| `get_scene_content_breakdown` | `limit?` (default 10), `sortBy?` (`triangles`/`materials`/`shaderVariants`/`drawCalls`/`visibleTriangles`) | Rendered content grouped by source model (GLTF `src` + one primitives row): triangles + share of scene, unique materials, shader variants, draw-call estimate, instances, renderers — plus each source's visible-from-this-POV subset (post-culling renderers, triangles, draw calls); position the camera first for viewpoint analysis |
| `get_performance_stats` | `sampleSeconds?` (default 2, max 10) | Holds the call while sampling real frame times: render FPS avg/min/max, hiccup frames (>50 ms), and the current scene's tick FPS vs target — pair with the breakdown tool for POV cost-vs-FPS analysis |
| `get_scene_logs` | `limit?`, `severity?`, `sinceSeq?` | Scene JS console output with monotonic sequence numbers for incremental polling |
| `list_scene_entities` | `limit?` | Entity ids of the current scene's ECS world |
| `get_entity_details` | `entityId` | All components of one scene entity |

### Controlling

| Tool | Arguments | Effect |
|---|---|---|
| `teleport` | `x`, `y`, `waitForReady?`, `timeoutSec?` | `/goto x,y` through the regular pipeline, waits for scene readiness |
| `move_to` | `x`, `y`, `z`, `lookAt{X,Y,Z}?`, `durationSec?` | Instant or smooth move to a world position (16 m per parcel) |
| `walk` | `directionX`, `directionY`, `seconds?`, `kind?`, `jump?` | Holds camera-relative movement through the real locomotion pipeline (collisions apply) |
| `look_at` | `x`, `y`, `z` | Rotates the camera to a world point (aim before a screenshot) |
| `set_camera_mode` | `mode` | Switches the camera mode like the user hotkey; refuses (with the reason) while a scene locks the camera — `CameraModeArea`, scene virtual camera, or photo camera. `get_player_state` → `camera.modeChangeAllowed` reports the lock state in advance |
| `set_camera_pose` | `x`,`y`,`z`, `lookAt{X,Y,Z}?`, `fov?`, `timeoutSec?` | Places the free camera at an absolute world position, optionally aiming it and setting FOV. Auto-enters free mode (same locks as `set_camera_mode`), waits for the blend to settle (`settled` in the result), and returns the actual pose. The camera stays put while the player moves; restore with `set_camera_mode` |
| `send_chat` | `message` | Sends to Nearby chat; `/commands` run through the chat command pipeline |
| `reload_scene` | `timeoutSec?` | Reloads the current scene (motion + skybox frozen during reload) |
| `trigger_emote` | `urn` or `stop: true`, `loop?` | Plays or stops an avatar emote |
| `click_entity` | `entityId` and/or `x`,`y`,`z` aim point, `button?`, `eventType?`, `timeoutSec?` | Presses a pointer button on a scene entity exactly like a real click: a camera-origin raycast validates the aim (occluders and the entity's `maxDistance` apply), then the entity's pointer-event intent is filled so the scene receives an identical `PBPointerEventsResult`. `click` sends down + up on consecutive scene ticks. Returns `hit`, hover text, hit point/distance, or the blocking entity |

### Interpreting the numbers

The content tools report *counts*, and some counts look scarier than they are. Guidance for drawing conclusions from them:

- **Materials ≠ draw-call cost.** The client renders with URP's SRP Batcher, which bins draws by **shader variant** (shader + enabled keywords) and keeps each material's properties in a persistent GPU buffer — so many materials sharing few variants render cheaply. Judge draw-call risk by `shaderVariants`, not `materials`. A high material count with a low variant count is a **memory and texture** concern (and a lost GPU-instancing opportunity, since instancing needs identical materials), not a frame-time concern.
- **`drawCallsEstimate` is pre-batching.** It counts material slots across renderers — an upper bound before the SRP Batcher and instancing reduce the real cost. Use it to compare sources against each other, not as an absolute GPU cost.
- **`shaderVariants` is a lower-bound proxy.** It counts distinct variant bins, not per-frame SetPass calls — the batcher only merges *consecutive* draws with the same variant, so interleaving can produce more switches than the bin count suggests. A low variant count reliably proves material dedup won't buy frame time; a high one flags shader churn worth consolidating.
- **The caps are soft.** The documented limits are warnings ("strong recommendations"), not enforced budgets. Correlate with measured cost — `get_performance_stats` at the relevant viewpoints — before prescribing optimizations.

## Structured output

`get_player_state`, `get_scene_state`, `get_scene_content_stats` and `list_scene_entities` also return `structuredContent` mirroring their text payload and declare a matching `outputSchema` in `tools/list` (MCP 2025-06-18). This is done **only as an example on the read-only state tools that benefit from it now** — every other tool returns text content only. A tool opts in by overriding `McpTool.OutputSchema` (default `null`); the same `McpJsonSchema` builder produces the schema.

## The scene-iteration loop

1. Serve the scene and launch the Explorer in one step: `npm run start -- --mcp` in the scene folder (serves at `http://127.0.0.1:8000`, auto-launches the installed client against it with the MCP server on, and hot-reloads on file changes).
2. To use a specific Explorer build instead, serve with `npm run start -- --no-client` and launch manually:

```bash
open Decentraland.app --args \
  --realm http://127.0.0.1:8000 --local-scene true --position 0,0 \
  --debug --skip-auth-screen --skip-version-check true \
  --mcp --windowed-mode --resolution 1280x720
```

Optional determinism flags for stable screenshots: `--disable-hud`, `--skybox-time-enabled false`, `--landscape-terrain-enabled false`, `--skip-minimum-specs-screen`.

3. The agent then loops: edit scene TypeScript → LSD hot reload applies it (or call `reload_scene`) → `get_scene_state` until ready → `screenshot` + `get_scene_logs` → verify → repeat.

Once loading completes, the server announces its address in the scene debug console (available with local scene development or `--scene-console`): `MCP server listening on http://127.0.0.1:8123/unity-explorer-mcp`. A startup failure (port in use) is announced there as an error instead. The same line lands in the `get_scene_logs` buffer, so agents can confirm the server from inside the loop.

A user-invokable Claude Code skill wrapping this loop lives at `.claude/skills/mcp-scene-iteration/` (invoke with `/mcp-scene-iteration`).

## Troubleshooting

- **Port already in use** — the server logs an `MCP` category error and stays inert; relaunch with a different `--mcp-port`. Multiple Explorer instances (`--multi-instance`) each need their own port. To confirm which process answers on a port, check `serverInfo.pid` in the `initialize` response and the `address` field of `get_player_state`.
- **HTTP 403** — the request carried a non-localhost `Origin` header; MCP clients and curl don't send one.
- **Server won't start on Windows** — `HttpListener` may require a URL ACL depending on machine policy: `netsh http add urlacl url=http://127.0.0.1:8123/unity-explorer-mcp/ user=Everyone` (elevated prompt), then relaunch.
- **Verbose logs** — enabling the server registers a scene-console log handler, which turns on unconditional verbose logging for the session (same behavior as `--scene-console`).
- **Scene entity dumps** — `list_scene_entities`/`get_entity_details` read the scene world without acquiring its sync lock (same as the existing `WorldInfoTool` debug tooling); treat results as a diagnostic snapshot.
- **`click_entity` returns `hit:false` with `blockedBy*`** — another collider sits on the camera→target line; `move_to`/`look_at` to a clear vantage and retry. If the reason is "out of range", close within the entity's `maxDistance` (default 10 m) first. Entities whose collider sits away from the pivot (GLTF meshes) may need an explicit `x/y/z` aim point.

## Implementation map

- `Explorer/Assets/DCL/McpServer/` — feature root, its own `DCL.McpServer` assembly. Two folders are folded into other assemblies via `.asmref` so they can reach code that assembly doesn't reference:
  - `Core/` — protocol, transport and tool contract: `McpHttpServer` (`HttpListener` server + Origin validation), `McpJsonRpcDispatcher` (JSON-RPC 2.0 routing; `PROTOCOL_VERSION` `2025-06-18`), `McpTool` (abstract tool base), `McpToolsRegistry`, `McpToolResult`, `McpToolAnnotations` (behaviour hints), `McpJsonSchema` (typed schema builder).
  - `Tools/` — one class per tool (16).
  - `Components/` — ECS components for the input-driving tools: `McpMovementOverride`, `McpPointerEventIntent`.
  - `Systems/` — **folded into `DCL.Plugins`** via `.asmref`: `McpServerPlugin` (builds the registry and hosts the server in `InjectToWorld`), `McpInputOverrideSystem` (held movement), `McpPointerEventSystem` (synthetic pointer press/release delivery; `ClickEntityTool` composes a click from two intents).
  - `Utils/` — `SceneLogBuffer`, `JObjectExtensions`.
  - `Tests/` — EditMode tests **folded into `DCL.EditMode.Tests`** via `.asmref`: dispatcher / registry / result routing and the pointer-click system.
- Gating: `FeatureId.MCP_SERVER` in `FeaturesRegistry` (resolved as `appArgs.HasFlag(MCP) || appArgs.HasFlag(MCP_PORT)`); `DynamicWorldContainer.CreateAsync` reads `FeaturesRegistry.Instance.IsEnabled(FeatureId.MCP_SERVER)` and adds `McpServerPlugin`.
- Flags: `AppArgsFlags.MCP` / `AppArgsFlags.MCP_PORT`; log category: `ReportCategory.MCP`.
