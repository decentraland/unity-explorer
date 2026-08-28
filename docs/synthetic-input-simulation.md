# Synthetic Input Simulation

The synthetic input simulation layer (`Explorer/Assets/DCL/SyntheticInput/`) lets an **automation driver** execute every input a human can — movement, pointer press/release on scene entities, hover, global SDK input actions, camera look, and UI interaction — through the **production input pipelines**, so collisions, occlusion, distance gates, scene input locks and the CRDT write-back are all the real ones.

Two driver front-ends share it:

- the **embedded MCP server** ([MCP Automation](mcp-automation.md)) — its input tools (`walk`, `click_entity`, `hover_entity`, `press_input`, `camera_look`, `ui_*`, …) are thin wrappers;
- the **AltTester static probes** ([Automation Testing](automation-testing.md)) — `WorldAutomationProbe` / `UiAutomationProbe`, called by the `explorer-automation` suites via `AltDriver.CallStaticMethod`.

Both execute the exact same code path, so a behavior verified through one front-end holds for the other.

---

## Design principle: semantic injection, not fake devices

For world/avatar input the layer does **not** synthesize operating-system-level device events. Instead it injects at the narrowest semantic seam of each production pipeline and lets everything downstream run unmodified:

| Input | Injection seam |
|---|---|
| Walk / jog / run / jump | `SyntheticMovementInputSystem` re-asserts `MovementInputComponent` **after** the real input systems each frame (and triggers the jump tick), so the hold survives their per-frame overwrite. Scene `InputModifier` locks apply exactly as they do to WASD (movement locks idle the hold, disabled kinds degrade through the same fallback table, jump locks drop the jump) unless the intent sets `IgnoreInputModifiers`. |
| Pointer aim + buttons on entities | `SyntheticPointerInput` (in `DCL.Interaction`, the pipeline's own contract surface): a single-frame post of an aim point and/or button edge. `PlayerOriginatedRaycastSystem` builds the reticle ray through the aim and echoes what it consumed; `ProcessPointerEventsSystem` applies the button edges under the same qualification gates as real input and clears the post. |
| Hover-only | The same aim post, re-posted each frame without a button until the hold expires — producing the same `PetHoverEnter`/`PetHoverLeave` flow a real cursor does. The leave is issued whenever the hover that is ending had been qualified, and is deliberately *not* re-qualified against the ray of the frame it ends on: that ray points elsewhere, and re-checking it against the target's `maxDistance` left tight-range targets hovered forever (fixed in `HoverFeedbackUtils`, matching the proximity-leave path). |
| Global SDK input actions | `PrepareGlobalInputEventsSystem` appends the synthetic button edges to the per-frame global-events buffer (deduped against real same-frame presses). The downstream suppression rule is untouched: an edge that lands entity-bound on a hovered, qualified entity suppresses the scene-root broadcast for that frame — exactly the real-key fan-out. |
| Camera look (relative) | `SyntheticCameraLookSystem` re-asserts `CameraInput.Delta` after `UpdateCameraInputSystem`, feeding the same Cinemachine axes as mouse-look. A `CameraBlockerComponent` suppresses it like real look input. |
| Camera look-at (absolute) | Translated into the production `CameraLookAtIntent`, then **refined**: that intent drives the rig's orbit value from an angle measured at the player's feet, which leaves a third-person camera with the right yaw and an aim that misses vertically, so `SyntheticCameraLookSystem` closes the residual through the look channel until the point is under the reticle. The loop is bounded — on target, out of frames, or as soon as the error stops improving (a clamped rig), and `look_at` reports the remaining `aimErrorDegrees`. |

UI interaction is **layered** (see below): a semantic path that synthesizes element events directly, and a virtual-device path for positional fidelity.

## Request lifecycle

Driver requests are ECS **intent components** installed on the player entity and fulfilled by the SyntheticInput systems, choreographed by `Core/EcsRequest`:

- `SendAsync` installs the intent and returns the task the fulfilling system completes. Installation is **last-write-wins**: a pending request of the same kind completes as *preempted*. One driver at a time is supported; concurrent drivers preempt each other.
- The system calls `CompleteAndRemove` (removal before completion, so continuations observe a clean entity).
- Timeouts live in the driver facade: a request the simulation never completed (paused Editor, dead scene) is abandoned via `AbandonAsync` and reported as timed out — nothing leaks.

| Intent | Lifetime |
|---|---|
| `SyntheticPointerInput` (pipeline post) | single frame, stamped with `PostedAtFrame`; stale posts are discarded unread |
| `SyntheticPointerEventIntent` | until observed one frame after injection (press/release ordered across scene ticks via the press handoff; hover re-posts until its hold expires) |
| `SyntheticMovementIntent` | held until `EndTime`, then movement restored to idle |
| `SyntheticCameraLookIntent` | held until `EndTime`, or until the camera consumed a look-at |
| `UiDeviceGestureRequest` | one virtual-device state per frame until the gesture's phase machine finishes |

## The driver facade

`SyntheticInputAgent` is the single entry point for world/avatar input (main-thread only; both the MCP dispatcher and `CallStaticMethod` already execute there):

```csharp
WalkAsync(axes, kind, seconds, jump, ignoreInputModifiers, ct)
CameraLookAsync(axisValue, seconds, ct)          LookAtAsync(worldTarget, ct)
ClickAsync / PointerDownAsync / PointerUpAsync(entityId, sceneId, aimPoint, screenPoint, button, timeoutSec, ct)
HoverAsync(entityId, sceneId, aimPoint, screenPoint, seconds, ct)
GlobalInputAsync(action, holdSeconds, entityId, sceneId, aimPoint, ct)
```

A click is composed from two intents — a press, then a release carrying the press handoff so the scene observes `PetDown` on an earlier tick than `PetUp`; a release that no longer reaches the press target reports the delivered press with the divergence (`upRayMissed`). Rich failure diagnostics (occluder entity, out-of-range distance, missing `PointerEvents`, scene changed mid-gesture) come from observing the pipeline's own raycast and hover state.

## UI simulation (`UiSimulation/`)

Two paths, both behind `UiAutomationServices` (one instance per automation session):

**Semantic (primary).** The target element is resolved first, then its events are synthesized directly:

- **Addressing** — client uGUI by transform path (`"MainUI/Sidebar/ExploreButton"`, `[n]` disambiguates same-named siblings, `(Clone)` suffixes are normalized away), by the instance id of the last `ui_list`, or by AltTester `AltId`. SDK scene UI **only by CRDT entity id** — UI Toolkit element names are built under `#if UNITY_EDITOR` and don't exist in player builds.
- **Occlusion pre-check** — a uGUI action requires the raycast's top hit to be the target (or inside it, or resolving its click to it); an SDK action requires `panel.Pick` to land in the target and no uGUI surface to raycast at the same point (a modal covering the scene UI). A covered element **fails with the cover's path** instead of being clicked through; `force` bypasses.
- **Mechanics** — uGUI via `ExecuteEvents` (enter → down → up → click → exit with a properly filled `PointerEventData`); SDK scene UI via UI Toolkit `SendEvent`. SDK clicks are **two-frame**: the scene's pointer-event slot (`UITransformComponent.PointerEventTriggered`) holds a single event drained by a throttled scene system, so the release waits until the press was consumed.

**Virtual devices (fidelity).** `AutomationVirtualDevices` registers a `DclAutomationMouse` + `DclAutomationKeyboard` for the session. Layout-path bindings resolve them into **both** input-action graphs — the serialized asset driving the UI input module and the `DCLInput.Instance` clone gameplay polls — which is the point: an injected state event behaves like a real device for every consumer. `UiVirtualDeviceGestureSystem` replays move/click/drag/key gestures one state per frame; while a pointer gesture runs, the frame-stamped `SyntheticCursorState` suppresses the cursor system's OS-cursor warps so nothing fights the injected positions. Pointer gestures require a free cursor (locked/panning fails the gesture rather than mutating the lock).

Coordinates in driver-facing payloads are **image pixels/fractions with the origin at the top-left** — the same way positions read off a screenshot — and are converted internally to Unity's bottom-left screen space.

## Enabling and gating

| Build | Launch | Result |
|---|---|---|
| any | `--mcp` / `--mcp-port` | `SyntheticInputPlugin` + MCP server; in `ALTTESTER` builds the probes are installed too |
| `ALTTESTER` build | `--alttester` | `SyntheticInputPlugin` + probes, no MCP server |
| release, no flags | — | nothing constructed: no systems, no virtual devices, zero cost |

Registration lives in `DynamicWorldContainer.CreateAsync`; the components/systems compile into all builds (only the `AltTester/` probes are `#if ALTTESTER`), activation is purely runtime. Log category: `ReportCategory.SYNTHETIC_INPUT`.

## AltTester front-end

`WorldAutomationProbe` and `UiAutomationProbe` (assembly **`DCL.SyntheticInput`** — the assembly name is part of the wire contract) expose the layer to the `explorer-automation` suites. `CallStaticMethod` is synchronous on the main thread, so multi-frame gestures use **start/poll**: a `Start*` method returns an operation id immediately and `PollJson(id)` reports `{"done":false}` until the payload is ready; instant semantic actions return their payload in one round-trip. Nothing throws towards the test — failures (internal timeouts included) come back as `{"ok":false,"error":...}` payloads, and a small ring keeps the most recent operations (an evicted id polls as an error).

```csharp
// synchronous semantic click:
altDriver.CallStaticMethod<string>(
    "DCL.SyntheticInput.AltTester.UiAutomationProbe", "ClickJson",
    "DCL.SyntheticInput", new object[] { "path", "MainUI/Sidebar/ExploreButton", "left", false });

// multi-frame gesture — start, then poll:
int op = altDriver.CallStaticMethod<int>(
    "DCL.SyntheticInput.AltTester.WorldAutomationProbe", "StartWalk",
    "DCL.SyntheticInput", new object[] { 0f, 1f, "jog", 2f, false, false });
// ... loop with a test-side timeout:
string json = altDriver.CallStaticMethod<string>(
    "DCL.SyntheticInput.AltTester.WorldAutomationProbe", "PollJson",
    "DCL.SyntheticInput", new object[] { op });
```

The probes are reflection-only entry points, preserved against IL2CPP stripping in [`Assets/link.xml`](../Explorer/Assets/link.xml).

## Documented divergences from human input

- `WalkAsync` moves the avatar but does not also emit `IA_FORWARD`-style global input events the way a physical W key does; compose `GlobalInputAsync(IaForward, hold)` alongside it when a scene listens for those.
- **An aimless `GlobalInputAsync` always reaches the scene root, never an entity.** The entity-bound half of the fan-out needs the reticle on a target, and the reticle follows the OS cursor — which no driver is holding over anything (`CursorState.Free` builds the ray from the cursor position, and a pointer over client UI is additionally excluded from hovering). Pass an aim (`entityId` / `aimPoint`) to produce the entity-bound edge; that is the driver's equivalent of pressing a key while looking at something.
- Suppression is *per scene tick*: an entity-bound edge suppresses the scene-root broadcast in the tick that writes it. Note that a scene reading `inputSystem.isTriggered(action, type)` **without** an entity is not measuring the scene-root broadcast at all — the SDK answers that from every entity's `PointerEventsResult`, so an entity-bound event satisfies it too. A scene verifying suppression must read `engine.RootEntity` explicitly.
- Hover tooltips pick their pressed/unpressed variant from the real Unity input actions, so a synthetically held button doesn't switch the tooltip glyph (cosmetic only).
- Synthetic camera look needs no OS cursor lock — a driver has no cursor to lock.
- Semantic UI clicks don't exercise hit-testing beyond their occlusion pre-check; use the device path (`ui_click device:true`, `ui_drag device:true`) when the pipeline itself is under test.
- **The virtual-device path drives the client UI stack, not UI Toolkit scene panels.** SDK scene UI consumes events sent to its elements, so an injected device pointer does not reach it: `ui_click device:true` on an SDK element reports whether the element observed anything (it normally does not, and says so instead of returning a bare success), and `ui_drag` delivers a drag that starts inside the scene UI semantically — press on the start element, moves along the path, release on the end element — unless `device:true` forces the device path.

## Extending the layer

- **New world-input capability** — add an intent component (implementing `IEcsRequest<TResult>`) + a delivering system in `Systems/` (folded into `DCL.Plugins`), expose it on `SyntheticInputAgent`, then add the thin MCP tool and probe method. Order the system against the production system it piggybacks on, and respect the same runtime gates real input obeys.
- **New gesture kind** — extend `UiDeviceGestureKind` and its phase machine in `UiVirtualDeviceGestureSystem` (one queued state per frame, phase state lives in the component).
- **New probe** — static, `#if ALTTESTER`, JSON-in/out, never throws; add its type to `Assets/link.xml` and its row to [Automation Testing](automation-testing.md).
- The MCP governance rules still apply: new tools are proposed via the skill's "Wanted tools" flow and implemented through plan mode.

## Testing

EditMode suites live in `SyntheticInput/Tests/` (folded into `DCL.EditMode.Tests`): the pointer delivery system (injection/observation/tick ordering/diagnostics — the regression keystone), movement holds incl. `InputModifier` parity, camera look, global-event appending (`Interaction/PlayerOriginated/Tests/`), the facade's gesture composition, UI addressing/occlusion/discovery logic, and the virtual-device gesture phase machine (`InputTestFixture`). The simulator's live event synthesis (uGUI `ExecuteEvents`, UI Toolkit `SendEvent`) is verified end-to-end against the running client through the MCP tools.
