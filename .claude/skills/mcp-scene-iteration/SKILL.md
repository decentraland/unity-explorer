---
name: mcp-scene-iteration
description: Iterate on SDK7 scenes against a running Explorer build via its embedded MCP server.
disable-model-invocation: true
---

# MCP Scene Iteration

Drive a running Explorer build through its embedded MCP server to build and test SDK7 scenes autonomously.

Full tool catalog and flag reference: [`docs/mcp-automation.md`](../../../docs/mcp-automation.md).

Deeper reference, loaded only when the task reaches it:

- [`reference/camera-and-movement.md`](reference/camera-and-movement.md) — before framing screenshots, free-camera sweeps, or navigating precise lines
- [`reference/assets.md`](reference/assets.md) — before placing, downloading, converting, or exporting any 3D model
- [`reference/visuals.md`](reference/visuals.md) — before tuning emissives/bloom, UI overlays, skybox time, or judging thin geometry

## Setup (once per session)

**Skill prerequisite — check before writing any scene code.** This skill only covers driving the Explorer; the SDK7 API knowledge (composite-first rule, component reference) lives in the `sdk-scenes` skill set, and parts of the API (e.g. native `TriggerArea`) are newer than training data — never write scene code from memory. If no `sdk-scenes`/`sdk-skills` skill is available in the session, stop and ask the user to install it from https://github.com/decentraland/sdk-skills:

```bash
npx skills add decentraland/sdk-skills --all       # run inside the scene folder (scene-local)
npx skills add decentraland/sdk-skills --all -g    # or globally (user-level, ~/.claude/skills)
```

Skills are loaded at session start, so a mid-session install may not surface until the session restarts.

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
   claude mcp add --transport http --scope user explorer http://127.0.0.1:8123/mcp
   ```

   Errors with "already exists in local config" if registered by a previous session — that's fine, nothing to do. If the current session has no `mcp__explorer__*` tools, follow "Missing tools" under Scene health & recovery below — the fix is the user reconnecting via `/mcp`, not a workaround.

4. Wait for the world to load: poll `get_scene_state` until `loadingScreenOn` is false and the scene reports `isReady: true`.

## The iteration loop

Repeat until **every requirement has proof**: a screenshot or state read demonstrating it, captured from a retail camera mode (`first_person`/`third_person`, not the free camera), with `get_scene_state` healthy and no unexplained errors in the logs.

1. **Edit** the scene TypeScript in `src/` — the LSD dev server hot-reloads the running Explorer within a few seconds. If you need a deterministic reset instead, call `reload_scene`.
2. **Confirm the scene is healthy**: `get_scene_state` — a `state` of `JavaScriptError` or `EcsError` means your code crashed the scene runtime.
3. **Read the runtime output**: `get_scene_logs` with `sinceSeq` set to the last sequence number you saw. Scene `console.log` output and exceptions land here.
4. **Look and verify**: position the view (`teleport`, `move_to`, `walk`, `look_at` — for precise framing or free-camera sweeps read [`reference/camera-and-movement.md`](reference/camera-and-movement.md)), then `screenshot` and inspect the image against what the scene code should produce.
5. **Exercise behavior**: `walk` into trigger areas, `click_entity` on interactables, and re-screenshot to verify reactions. `list_scene_entities` + `get_entity_details` show the scene's ECS state when visuals aren't enough.

**Cross-examine** every conclusion: confirm each visual claim with a state read (ECS values via `get_entity_details`, logs, `get_player_state` position), and each state claim with pixels. One channel lies routinely — colliders exist that pixels don't show, entities render invisible while their state looks healthy, animations silently don't play. The reference files call out where cross-examination is mandatory.

## Screenshot frequency & cost

Every screenshot returned by the MCP `screenshot` tool lands in your context as an image (~1.2k tokens at 1280×720, scaling with pixel count). Occasional captures through the tool are fine; **frequent or burst captures must go through the bundled script instead**, which saves frames to disk (zero context cost) and prints only the caption:

```bash
scripts/screenshot.sh -o shot.jpg              # single frame to a file
scripts/screenshot.sh -n 10 -i 0.5             # burst: 10 frames every 0.5s into mcp-shots/ (time-based behavior: tweens, animations)
scripts/screenshot.sh -w 640                   # cheap sanity-check resolution (~4x fewer tokens when you Read it)
scripts/screenshot.sh --world-only --png       # UI-less lossless frame
```

Paths are relative to this skill's directory; requires curl + python3; pass `-p <port>` when not on 8123. Then `Read` only the frames you actually need to inspect — capture many, look at few. For before/after comparisons, capture both to disk and read just those two. Use `maxWidth` 640 for quick checks and 1280 only for final verification. Captures are serialized server-side (concurrent requests are rejected), so keep burst intervals ≥ 0.2s.

## Scene health & recovery

- Sequence-poll logs (`sinceSeq`) instead of re-reading the whole buffer; errors survive in the buffer even if they scrolled by.
- `scene.json` changes (parcels, spawn points) are not hot-reloaded — restart the `npm run start` process, then `reload_scene`.
- After `teleport` or `reload_scene`, always re-check `get_scene_state` before interacting; readiness can lag a few seconds.
- One parcel is 16×16 m; parcel `(x, y)` spans world positions `(16x..16x+16, 16y..16y+16)`. `--position 0,0` spawns at parcel 0,0.
- If the connection drops, the build probably crashed or was closed — relaunch it with the same flags; the MCP endpoint URL stays the same.
- **Missing tools**: `mcp__explorer__*` tools absent in-session are recoverable (typically the Explorer wasn't running when the session started, so the registered server failed its startup connection). Ask the user to run `/mcp` and reconnect the `explorer` server — an interactive command only the user can run; a successful reconnect binds all the server's tools into the running session (verified). A plain `claude mcp add` mid-session does NOT surface tools by itself. Last resort: drive the endpoint directly with curl JSON-RPC (`POST /mcp`, methods `initialize` then `tools/call`; responses may be SSE-framed, tool payloads are JSON in `result.content[0].text`, screenshots are base64 in image content blocks).
- After a hot reload the player can end up off-parcel (e.g. parcel `0,-1`); `get_scene_state` then reports a null scene and `reload_scene` fails with "no scene at the current parcel". Check `get_player_state` → `parcel`, `move_to` back inside, and the scene loads again.
- Each file save triggers a rebuild: editing usage and import in separate saves produces a transient `SceneError: X is not defined` between them. Write new modules before wiring them in, and prefer a single whole-file write for multi-part edits to one file.
- **Rapid successive saves can HARD-WEDGE the client (verified 2026-07-10).** Two saves seconds apart made the Explorer load a mid-write bundle → `SyntaxError: Invalid or unexpected token` at scene start → the scene facade is torn down and drops out of `ScenesCache`, and `get_scene_state` reports `scene: null` while standing on the parcel. From that state NOTHING recovers in-session: `reload_scene` errors ("no scene at the current parcel" — its guard and every underlying reload path need the scene still cached), `/reload` hangs until cancelled, the minimap RELOAD SCENE button just sends `/reload`, LSD file-save pushes no-op (`TryGetBySceneId` misses), and moving far off-parcel and back does not recreate the facade. Only exiting/re-entering play mode (editor) or relaunching the standalone build recovers. Prevention: batch multi-edit changes into ONE file write, and after any save landing seconds after a previous one, verify `get_scene_state` still shows a scene before saving again.
- The `teleport` tool silently no-ops in local-scene-development mode: `/goto` teleports are disallowed there (chat shows "Teleport is not allowed in local scene development mode") but the tool still answers "Arrived at (x,y)". Use `move_to` for repositioning in LSD sessions.
- The Explorer under test may be the **Unity Editor in play mode**, not a standalone Decentraland.app — check `ps aux | grep -i unity` before considering a relaunch. Never kill the editor process; recovery from a wedged client is then a user action (exit/re-enter play mode).

## Interaction testing

- `click_entity` presses a pointer button on a scene entity (get ids from `list_scene_entities`). The target needs a `PointerEvents` component and a collider; the aim is validated by a real camera-origin raycast, so occluders return `hit:false` + `blockedBy*` (reposition and retry) and the entity's `maxDistance` (default 10 m) applies — get close first. `upRayMissed: true` means the target moved between press and release (e.g. a door starting to swing) and the release was delivered with the press-frame hit. For GLTF entities whose collider sits away from the pivot, pass an explicit `x/y/z` aim point. The player must be standing on the scene's parcel — off-parcel clicks fail with "no running current scene".
- `walk` moves relative to the camera and requires an explicit direction: pass `directionY: 1` for forward (`directionX` strafes); omitting both errors with "directionX and directionY must not both be zero".
- Collider checks beat pixels for physics (cross-examine): `look_at` straight at the target, `walk` forward, then compare `get_player_state` positions to prove passage or blockage.
- Trigger areas fire `onTriggerEnter` immediately after `reload_scene` if the player is already standing inside one — reposition the player outside all triggers before testing enter/exit sequencing (and treat post-reload trigger logs as stale state, not gameplay).

## Improving this skill

Two rules:

**1. Learned something the skill didn't tell you?** A flag that turned out to be required, a timing quirk, a better verification pattern, or information here that proved wrong — edit this skill in place before finishing your session. Keep additions terse and verified (facts you observed, not speculation), and file them where their branch lives: setup, health/recovery, and interaction facts in this SKILL.md; camera and navigation facts in `reference/camera-and-movement.md`; model and import facts in `reference/assets.md`; rendering and visual-tuning facts in `reference/visuals.md`. The canonical copy lives in the unity-explorer repo at `.claude/skills/mcp-scene-iteration/`; if you are running from a copy (e.g. `~/.claude/skills/`), apply the same edit to the canonical copy too when the repo is accessible, or tell the user to sync it.

**2. Missing a capability?** If the loop is blocked because no existing MCP tool can do what you need (e.g. pressing a specific key, reading a value no tool exposes), do NOT work around it by modifying the Explorer client, the MCP server, or the unity-explorer repo yourself. Stop and prompt the user with a concrete tool proposal:

- proposed tool name and one-line purpose
- input arguments (names, types, defaults)
- expected output shape
- the blocked use case, and why the existing tools can't cover it

The user decides whether and when to implement it. **MANDATORY: implementing an approved tool must go through plan mode first** — whichever session does the implementation starts in plan mode, researches the unity-explorer codebase (the server lives under `Explorer/Assets/DCL/Mcp/` — see `docs/mcp-automation.md` → Implementation map), and presents the plan for user approval before writing any code. Also append the proposal to the "Wanted tools" list below so it isn't lost if the user defers.

## Wanted tools

Proposals from agent sessions — name, purpose, blocked use case. Remove entries once implemented.

- **recover_scene** — force-recreate the scene at the player's parcel when it has dropped out of `ScenesCache` (`get_scene_state` → `scene: null`; the hard-wedge state described above, where every existing reload path needs the cached facade and the session is dead until the user restarts play mode). Inputs: `timeoutSec?: number` (default 30). Output: same shape as `reload_scene`. Implementation lead: clear failed `AssetPromise<ISceneFacade, GetSceneFacadeIntention>` state on the definition entity and reset `StaticScenePointers.Promise` on the realm entity so the static-pointer systems re-resolve — the same reset `ECSReloadScene.DisposeAndRestartAsync` already performs for LSD, minus the requirement that a live scene exists.
