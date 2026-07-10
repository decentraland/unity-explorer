# Camera & movement reference

Read before framing screenshots, navigating precise lines, or inspecting a build from multiple viewpoints.

## Aiming the camera

- `move_to`'s `lookAt*` params orient the avatar but NOT the third-person camera; `screenshot` and `walk` follow the camera. Call the standalone `look_at` tool (it aligns camera yaw — confirm via `get_player_state` → `camera.rotationEuler.y`) before walking a precise line or framing a screenshot.
- `look_at` lines the third-person camera up through the avatar, so the avatar occludes exactly the thing you framed. To photograph a subject, first `move_to` a spot offset sideways from the camera→subject line, then `look_at`.

## Free camera

- The free camera is the fastest way to inspect a scene from many points of view: `set_camera_pose` places it at any absolute position, optionally aims it (`lookAt*`) and sets `fov`, auto-entering free mode. Repositioning while already free is instant (~200ms), so sweep a build cheaply — aerial plan view, each facade, eye-level details, interiors — capturing to disk between calls, instead of walking the player around. `look_at` also works in free mode (aims from the camera's own position), and the free camera stays put while the player moves, so you can even watch the avatar walk through the scene from a fixed vantage. Entering free from another mode blends over ~2-3s (the tool waits and reports `settled`).
- The free camera is a debug view, not what players see. To confirm the end-user experience, switch back to the real modes — `set_camera_mode` `first_person` / `third_person` / `drone` are exactly the cameras retail users have — and re-check framing, avatar occlusion, and interaction reach from there (e.g. verify a hover target is actually visible and clickable at player height, not just from a flattering freecam angle). Restore a player-following view with `set_camera_mode third_person` when done. `set_camera_mode` respects scene locks and errors truthfully — check `get_player_state` → `camera.modeChangeAllowed` first; `false` inside a `CameraModeArea`/scene virtual camera is correct behavior worth verifying, not a tool failure. `screenshot` works in any mode.

## Precise navigation

- Collider-bump navigation moves the player along precise lines reliably: `look_at` a point past where you want to end up, `walk` with generous `seconds`, and let a blocking collider stop the player — the returned `endPosition` lands ~0.38m (capsule radius) short of the collider face, a deterministic waypoint for the next leg with no duration tuning. Timing legs precisely is fragile (jog ramp-up varies effective speed); overshooting into a collider is not. Only a final leg through a gap with nothing beyond it needs a tight duration, or the player runs off-parcel.
- Measured locomotion speeds (flat ground): jog ≈6.5 m/s once ramped, `kind: "walk"` ≈1.4 m/s. To stop at a precise point no collider will stop you at, take one timed jog leg, read `endPosition`, then micro-correct with 0.5-1.2s `kind: "walk"` legs — position feedback plus slow corrections converges in 1-2 iterations where a single timed jog leg won't.
