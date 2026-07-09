---
name: mcp-scene-iteration
description: Iterate on a Decentraland SDK7 scene against a running Explorer build through its embedded MCP server - launch the build with the right flags, connect, then loop edit, reload, screenshot, and log-check until the scene works.
disable-model-invocation: true
---

# MCP Scene Iteration

Drive a running Explorer build through its embedded MCP server to build and test SDK7 scenes autonomously: you can see the rendered result (screenshots), read scene runtime errors (JS console logs), and control the player (teleport, walk, look, chat commands).

Full tool catalog and flag reference: [`docs/mcp-automation.md`](../../../docs/mcp-automation.md).

## Setup (once per session)

0. **Probe for an already-running setup first.** The Explorer and dev server are often already up from a previous session — check before launching anything:

   ```bash
   # MCP server up? (Explorer running with --mcp)
   curl -s -m 2 http://127.0.0.1:8123/mcp -X POST \
     -H 'Content-Type: application/json' -H 'Accept: application/json, text/event-stream' \
     -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"probe","version":"1"}}}'
   # Dev server up, and serving the RIGHT scene folder?
   lsof -nP -i :8000 -sTCP:LISTEN   # then check the PID's cwd or command path
   ```

   If the MCP probe answers with a `serverInfo` result, skip step 2 (and step 3 if `mcp__explorer__*` tools are already available). If port 8000 is served **from the target scene folder**, skip step 1; if it serves a different folder, kill that process and serve the right one. Only do the steps below for whatever is actually missing.

1. **Serve the scene locally** from the scene folder (keep it running in the background):

   ```bash
   npm install && npm run start
   ```

   This serves the scene at `http://127.0.0.1:8000` and hot-reloads it in the connected Explorer whenever a source file changes. Close any Explorer/launcher window it auto-opens if you manage your own build.

2. **Launch the Explorer** connected to that scene with the MCP server enabled (only if the step-0 probe found nothing on 8123):

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

   Errors with "already exists in local config" if registered by a previous session — that's fine, nothing to do. If the current session has no `mcp__explorer__*` tools (typically because the Explorer wasn't running when the session started, so the registered server failed its startup connection), don't jump straight to workarounds: **ask the user to run `/mcp` and reconnect the `explorer` server** — that's an interactive command only the user can run, and a successful reconnect binds all the server's tools into the running session (verified). Fall back to curl JSON-RPC (Tips section) only if reconnecting isn't possible or doesn't surface the tools.

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
- Missing `mcp__explorer__*` tools in-session are recoverable: the user running `/mcp` and reconnecting the `explorer` server binds its tools into the running session (verified — works when the server was registered but showed as failed because the Explorer wasn't up at session start). Ask the user to do that first; `/mcp` is interactive and cannot be run by the agent. As a last resort, drive the endpoint directly with curl JSON-RPC (`POST /mcp`, methods `initialize` then `tools/call`; responses may be SSE-framed, tool payloads are JSON in `result.content[0].text`, screenshots are base64 in image content blocks). A plain `claude mcp add` mid-session does NOT surface tools by itself.
- `move_to`'s `lookAt*` params orient the avatar but NOT the third-person camera; `screenshot` and `walk` follow the camera. Call the standalone `look_at` tool (it aligns camera yaw — confirm via `get_player_state` → `camera.rotationEuler.y`) before walking a precise line or framing a screenshot.
- After a hot reload the player can end up off-parcel (e.g. parcel `0,-1`); `get_scene_state` then reports a null scene and `reload_scene` fails with "no scene at the current parcel". Check `get_player_state` → `parcel`, `move_to` back inside, and the scene loads again.
- Each file save triggers a rebuild: editing usage and import in separate saves produces a transient `SceneError: X is not defined` between them. Write new modules before wiring them in, and prefer a single whole-file write for multi-part edits to one file.
- `click_entity` presses a pointer button on a scene entity (get ids from `list_scene_entities`). The target needs a `PointerEvents` component and a collider; the aim is validated by a real camera-origin raycast, so occluders return `hit:false` + `blockedBy*` (reposition and retry) and the entity's `maxDistance` (default 10 m) applies — get close first. `upRayMissed: true` means the target moved between press and release (e.g. a door starting to swing) and the release was delivered with the press-frame hit. For GLTF entities whose collider sits away from the pivot, pass an explicit `x/y/z` aim point. The player must be standing on the scene's parcel — off-parcel clicks fail with "no running current scene".
- `walk` moves relative to the camera and requires an explicit direction: pass `directionY: 1` for forward (`directionX` strafes); omitting both errors with "directionX and directionY must not both be zero".
- Composite-authored box primitives need `"box": {"uvs": []}` in `core::MeshRenderer` — a bare `"box": {}` crashes `sdk-commands build` with `TypeError: message.uvs is not iterable`.
- Collider checks beat pixels for physics: `look_at` straight at the target, `walk` forward, then compare `get_player_state` positions to prove passage or blockage.
- Collider-bump navigation moves the player along precise lines reliably: `look_at` a point past where you want to end up, `walk` with generous `seconds`, and let a blocking collider stop the player — the returned `endPosition` lands ~0.38m (capsule radius) short of the collider face, a deterministic waypoint for the next leg with no duration tuning. Timing legs precisely is fragile (jog ramp-up varies effective speed); overshooting into a collider is not. Only a final leg through a gap with nothing beyond it needs a tight duration, or the player runs off-parcel.
- Measured locomotion speeds (flat ground): jog ≈6.5 m/s once ramped, `kind: "walk"` ≈1.4 m/s. To stop at a precise point no collider will stop you at, take one timed jog leg, read `endPosition`, then micro-correct with 0.5-1.2s `kind: "walk"` legs — position feedback plus slow corrections converges in 1-2 iterations where a single timed jog leg won't.
- Trigger areas fire `onTriggerEnter` immediately after `reload_scene` if the player is already standing inside one — reposition the player outside all triggers before testing enter/exit sequencing (and treat post-reload trigger logs as stale state, not gameplay).
- The free camera is the fastest way to inspect a scene from many points of view: `set_camera_pose` places it at any absolute position, optionally aims it (`lookAt*`) and sets `fov`, auto-entering free mode. Repositioning while already free is instant (~200ms), so sweep a build cheaply — aerial plan view, each facade, eye-level details, interiors — capturing to disk between calls, instead of walking the player around. `look_at` also works in free mode (aims from the camera's own position), and the free camera stays put while the player moves, so you can even watch the avatar walk through the scene from a fixed vantage. Entering free from another mode blends over ~2-3s (the tool waits and reports `settled`).
- The free camera is a debug view, not what players see. To confirm the end-user experience, switch back to the real modes — `set_camera_mode` `first_person` / `third_person` / `drone` are exactly the cameras retail users have — and re-check framing, avatar occlusion, and interaction reach from there (e.g. verify a hover target is actually visible and clickable at player height, not just from a flattering freecam angle). Restore a player-following view with `set_camera_mode third_person` when done. `set_camera_mode` respects scene locks and errors truthfully — check `get_player_state` → `camera.modeChangeAllowed` first; `false` inside a `CameraModeArea`/scene virtual camera is correct behavior worth verifying, not a tool failure. `screenshot` works in any mode.
- `look_at` lines the third-person camera up through the avatar, so the avatar occludes exactly the thing you framed. To photograph a subject, first `move_to` a spot offset sideways from the camera→subject line, then `look_at`.
- Thin geometry (a door panel ~0.1m) is invisible edge-on: a panel "open" at ~90-110° reads as a sliver from the front. Verify pose via ECS rotation (`get_entity_details` quaternion) as well as pixels, and prefer open angles like ~135° when front visibility matters.
- When picking from the sdk-skills model catalog, fetch the `preview:` thumbnail BEFORE downloading — some models render near-black or broken even in their own previews (e.g. arcade-cabinet-atari). Use the exact `[anim: ...]` clip names; a wrong clip name fails silently (no error, no motion) — burst-capture (`-n 3 -i 1`) and diff frames to prove an animation is actually running.
- Catalog dims/pivots lie: bounding boxes can include baked animation paths or outline meshes, pivots can sit mid-crown (palms) or nowhere near the mesh (a baked drive path carried one car's mesh entirely off-parcel — swap such models rather than debug them). Place → screenshot → adjust is faster than trusting listed sizes.
- Blender-authored GLBs work end-to-end: export with `bpy.ops.export_scene.gltf(use_selection=True)` straight into the scene's Models folder (hot-loads like any file change). Set the object origin to bottom-center before export (`cursor to (0,0,0)` + `origin_set(type='ORIGIN_CURSOR')` with geometry built up from z=0) so `position.y = 0` grounds the model, and `transform_apply` rotation/scale for clean transforms. Principled BSDF emissive (Emission Color + Strength) renders as expected in Explorer, including the zero-channel neon saturation rule.
- Converting downloaded FBX/OBJ to GLB in Blender works, with three verified traps: (1) FBX materials can import with Principled Alpha = 0 (FBX transparency-factor quirk, seen on Quaternius packs) — the GLB then has `alphaMode: MASK` with baseColor alpha 0 and the model is INVISIBLE in Explorer while its entity, tween and logs all look healthy; force Alpha=1 + `blend_method='OPAQUE'` before export, and when a GLB renders nothing, parse its JSON chunk (nodes/materials) instead of guessing. (2) The glTF exporter's default animation mode exports EVERY action in the .blend that fits the armature, so clips from other imported models leak into each GLB; use `export_animation_mode='ACTIVE_ACTIONS'` with the right action active (exports one clip named `Animation`). (3) Some kits (e.g. Kenney furniture) ship ASCII FBX which Blender refuses — run `file *.fbx` first and fall back to the kit's OBJ folder. Skinned meshes respect entity Transform scale, so oversize rigs can be scaled at the entity.
- Free CC0 model sources that download cleanly via curl: kenney.nl (zip URL is on the asset page, FBX+OBJ+GLTF inside), and itch.io free packs via the scripted flow: POST `<game>/download_url` with the page's csrf_token → GET the returned key URL → grab `data-upload_id` → POST `<game>/file/<id>?source=game_download` → signed CDN URL (expires ~60s, download immediately).
- Downloaded GLBs into the scene folder hot-load without restarting the dev server. Many props ship with no colliders — walk onto them and check the player's `y` via `get_player_state`; add `visibleMeshesCollisionMask: 3` for anything that should be solid.
- `screenshot`'s `worldOnly` (and the script's `--world-only`) renders with full post-processing, bloom included (fixed + verified 2026-07-06: the capture target was LDR, which clamped emissives and starved bloom — it is HDR now). Emissive/glow looks are judgeable from world-only frames, with one caveat: halo spread reads slightly tighter than the full-view capture (render-resolution difference), so do final pixel-exact glow tuning on full-view frames.
- Neon emissive recipe (verified with bloom, sunset skybox): pin one emissive channel to exactly 0 (e.g. `(1, 0, 0.6)` magenta) — a small minor channel (0.15-0.2) times `emissiveIntensity` 6+ whites the whole surface out. Then weight by luminance and background: green-heavy hues (cyan) bloom to white far faster than magenta and additively desaturate against the pink haze (cyan + pink = white), so dim or blue-shift them (`(0, 0.35, 0.8)` works); magenta reinforces the sunset and stays saturated at intensity 6-15. A large on-screen emitter always whites out at its core — the hue lives in the halo, which is what a real neon tube looks like; judge hue from gameplay distance, not close-ups. Don't add alpha-blended "glow shell" boxes around emitters — bloom already provides the halo, and the shell just reads as a grey display case.
- `SkyboxTime.create(engine.RootEntity, { fixedTime })` pins the scene to a permanent time of day regardless of launch flags.

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
