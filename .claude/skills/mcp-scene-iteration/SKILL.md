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

## Tips

- Sequence-poll logs (`sinceSeq`) instead of re-reading the whole buffer; errors survive in the buffer even if they scrolled by.
- `scene.json` changes (parcels, spawn points) are not hot-reloaded — restart the `npm run start` process, then `reload_scene`.
- After `teleport` or `reload_scene`, always re-check `get_scene_state` before interacting; readiness can lag a few seconds.
- One parcel is 16×16 m; parcel `(x, y)` spans world positions `(16x..16x+16, 16y..16y+16)`. `--position 0,0` spawns at parcel 0,0.
- If the connection drops, the build probably crashed or was closed — relaunch it with the same flags; the MCP endpoint URL stays the same.
