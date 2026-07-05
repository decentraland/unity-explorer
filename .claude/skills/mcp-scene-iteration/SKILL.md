---
name: mcp-scene-iteration
description: Iterate on a Decentraland SDK7 scene against a running Explorer build through its embedded MCP server - launch the build with the right flags, connect, then loop edit, reload, screenshot, and log-check until the scene works.
disable-model-invocation: true
---

# MCP Scene Iteration

Drive a running Explorer build through its embedded MCP server to build and test SDK7 scenes autonomously: you can see the rendered result (screenshots), read scene runtime errors (JS console logs), and control the player (teleport, walk, look, chat commands).

Full tool catalog and flag reference: [`docs/mcp-automation.md`](../../../docs/mcp-automation.md).

## Setup (once per session)

1. **Serve the scene locally** from the scene folder (keep it running in the background):

   ```bash
   npm install && npm run start
   ```

   This serves the scene at `http://127.0.0.1:8000` and hot-reloads it in the connected Explorer whenever a source file changes. Close any Explorer/launcher window it auto-opens if you manage your own build.

2. **Launch the Explorer** connected to that scene with the MCP server enabled:

   ```bash
   # macOS
   open /path/to/Decentraland.app --args \
     --realm http://127.0.0.1:8000 --local-scene true --position 0,0 \
     --debug --skip-auth-screen --skip-version-check true \
     --mcp --windowed-mode --resolution 1280x720
   ```

   On Windows call `Decentraland.exe` with the same arguments. Add `--disable-hud --skybox-time-enabled false --landscape-terrain-enabled false` when you want deterministic screenshots without HUD noise.

3. **Connect the MCP server** (default port 8123):

   ```bash
   claude mcp add --transport http explorer http://127.0.0.1:8123/mcp
   ```

4. Wait for the world to load: poll `get_scene_state` until `loadingScreenOn` is false and the scene reports `isReady: true`.

## The iteration loop

Repeat until the scene meets the requirements:

1. **Edit** the scene TypeScript in `src/` — the LSD dev server hot-reloads the running Explorer within a few seconds. If you need a deterministic reset instead, call `reload_scene`.
2. **Confirm the scene is healthy**: `get_scene_state` — a `state` of `JavaScriptError` or `EcsError` means your code crashed the scene runtime.
3. **Read the runtime output**: `get_scene_logs` with `sinceSeq` set to the last sequence number you saw. Scene `console.log` output and exceptions land here.
4. **Look and verify**: position the view (`teleport`, `move_to`, `walk`, `look_at`), then `screenshot` and inspect the image against what the scene code should produce.
5. **Exercise behavior**: `walk` into trigger areas, `send_chat` for commands, `trigger_emote`, and re-screenshot to verify reactions. `list_scene_entities` + `get_entity_details` show the scene's ECS state when visuals aren't enough.

## Screenshot frequency & cost

Every screenshot returned by the MCP `screenshot` tool lands in your context as an image (~1.2k tokens at 1280×720, scaling with pixel count). Occasional captures through the tool are fine; **frequent or burst captures must go through the bundled script instead**, which saves frames to disk (zero context cost) and prints only the caption:

```bash
scripts/screenshot.sh -o shot.jpg              # single frame to a file
scripts/screenshot.sh -n 10 -i 0.5             # burst: 10 frames every 0.5s into mcp-shots/ (time-based behavior: tweens, animations)
scripts/screenshot.sh -w 640                   # cheap sanity-check resolution (~4x fewer tokens when you Read it)
scripts/screenshot.sh --world-only --png       # UI-less lossless frame
```

Paths are relative to this skill's directory; requires curl + python3; pass `-p <port>` when not on 8123. Then `Read` only the frames you actually need to inspect — capture many, look at few. For before/after comparisons, capture both to disk and read just those two. Use `maxWidth` 640 for quick checks and 1280 only for final verification. Captures are serialized server-side (concurrent requests are rejected), so keep burst intervals ≥ 0.2s.

## Tips

- Sequence-poll logs (`sinceSeq`) instead of re-reading the whole buffer; errors survive in the buffer even if they scrolled by.
- `scene.json` changes (parcels, spawn points) are not hot-reloaded — restart the `npm run start` process, then `reload_scene`.
- After `teleport` or `reload_scene`, always re-check `get_scene_state` before interacting; readiness can lag a few seconds.
- One parcel is 16×16 m; parcel `(x, y)` spans world positions `(16x..16x+16, 16y..16y+16)`. `--position 0,0` spawns at parcel 0,0.
- If the connection drops, the build probably crashed or was closed — relaunch it with the same flags; the MCP endpoint URL stays the same.
- `claude mcp add` only takes effect for the NEXT Claude Code session. If the server was registered mid-session, its tools are not loadable in-session — drive the endpoint directly with curl JSON-RPC (`POST /mcp`, methods `initialize` then `tools/call`; responses may be SSE-framed, tool payloads are JSON in `result.content[0].text`, screenshots are base64 in image content blocks).
- `move_to`'s `lookAt*` params orient the avatar but NOT the third-person camera; `screenshot` and `walk` follow the camera. Call the standalone `look_at` tool (it aligns camera yaw — confirm via `get_player_state` → `camera.rotationEuler.y`) before walking a precise line or framing a screenshot.
- After a hot reload the player can end up off-parcel (e.g. parcel `0,-1`); `get_scene_state` then reports a null scene and `reload_scene` fails with "no scene at the current parcel". Check `get_player_state` → `parcel`, `move_to` back inside, and the scene loads again.
- Each file save triggers a rebuild: editing usage and import in separate saves produces a transient `SceneError: X is not defined` between them. Write new modules before wiring them in, and prefer a single whole-file write for multi-part edits to one file.
- `click_entity` presses a pointer button on a scene entity (get ids from `list_scene_entities`). The target needs a `PointerEvents` component and a collider; the aim is validated by a real camera-origin raycast, so occluders return `hit:false` + `blockedBy*` (reposition and retry) and the entity's `maxDistance` (default 10 m) applies — get close first. `upRayMissed: true` means the target moved between press and release (e.g. a door starting to swing) and the release was delivered with the press-frame hit. For GLTF entities whose collider sits away from the pivot, pass an explicit `x/y/z` aim point. The player must be standing on the scene's parcel — off-parcel clicks fail with "no running current scene".
- Collider checks beat pixels for physics: `look_at` straight at the target, `walk` forward, then compare `get_player_state` positions to prove passage or blockage.
- Camera mode shapes what screenshots show: `set_camera_mode` switches `first_person` (cleanest for aiming at what's directly ahead — no avatar occlusion), `third_person`, `drone` (higher, wider) and `free`. It respects scene locks and errors truthfully — check `get_player_state` → `camera.modeChangeAllowed` first; `false` inside a `CameraModeArea`/scene virtual camera is correct behavior worth verifying, not a tool failure. `free` detaches the camera for framing but ANY player movement (`walk`, `move_to`) drops it back to `third_person`, so frame first, capture, then move.
- `look_at` lines the third-person camera up through the avatar, so the avatar occludes exactly the thing you framed. To photograph a subject, first `move_to` a spot offset sideways from the camera→subject line, then `look_at`.
- Thin geometry (a door panel ~0.1m) is invisible edge-on: a panel "open" at ~90-110° reads as a sliver from the front. Verify pose via ECS rotation (`get_entity_details` quaternion) as well as pixels, and prefer open angles like ~135° when front visibility matters.

## Improving this skill

This skill is expected to evolve as agents use it. Two rules:

**1. Learned something the skill didn't tell you?** A flag that turned out to be required, a timing quirk, a better verification pattern, or information here that proved wrong — edit this SKILL.md in place before finishing your session. Keep additions terse and verified (facts you observed, not speculation). The canonical copy lives in the unity-explorer repo at `.claude/skills/mcp-scene-iteration/SKILL.md`; if you are running from a copy (e.g. `~/.claude/skills/`), apply the same edit to the canonical copy too when the repo is accessible, or tell the user to sync it.

**2. Missing a capability?** If the loop is blocked because no existing MCP tool can do what you need (e.g. clicking a scene entity, pressing a specific key, reading a value no tool exposes), do NOT work around it by modifying the Explorer client, the MCP server, or the unity-explorer repo yourself. Stop and prompt the user with a concrete tool proposal:

- proposed tool name and one-line purpose
- input arguments (names, types, defaults)
- expected output shape
- the blocked use case, and why the existing tools can't cover it

The user decides whether and when to implement it. **MANDATORY: implementing an approved tool must go through plan mode first** — whichever session does the implementation starts in plan mode, researches the unity-explorer codebase (the server lives under `Explorer/Assets/DCL/Mcp/` — see `docs/mcp-automation.md` → Implementation map), and presents the plan for user approval before writing any code. Also append the proposal to the "Wanted tools" list below so it isn't lost if the user defers.

## Wanted tools

Proposals from agent sessions — name, purpose, blocked use case. Remove entries once implemented.

- (none yet)
