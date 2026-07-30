# MCP Automation Server

The Explorer can host an embedded [MCP (Model Context Protocol)](https://modelcontextprotocol.io/) server so coding agents (e.g. Claude Code) can **see** the running client (screenshots, player/scene state, scene console logs, HTTP activity, the live UI hierarchy) and **control** it (teleport, move, walk, look, chat commands, scene reload, and the client's own UI — clicks, text entry, keys, scrolling) — closing the edit → reload → verify loop for SDK7 scene development without a human in the middle, and giving a UI test suite a driver that needs no instrumented build.

The server is compiled into all builds — Creator Tools drives the installed client through it — but stays dormant unless explicitly enabled at launch. The client-UI automation and reflection tools are the exception: they exist to drive the client's own UI from a test suite, and are compiled out of release builds.

---

## Enabling

| Flag | Effect |
|---|---|
| `--mcp` | Starts the MCP server on the default port **8123** |
| `--mcp-port <port>` | Starts the MCP server on a specific port (implies `--mcp`) |
| `--mcp-reflection` | Adds the three reflection tools to the server's tool list. Inert without `--mcp`/`--mcp-port`, and without the `MCP_TEST_AUTOMATION` build (see [Security model](#security-model)) |

The flags are accepted **from the command line only**. `DeepLinkAllowlist` is deny-by-default and lists none of them, so `ProcessDeepLinkParameters` drops them (with a "Dropped non-allowlisted deep-link param(s)" warning) and a `decentraland://` link cannot start the server. That is deliberate — see [Security model](#security-model). The endpoint is `http://127.0.0.1:<port>/unity-explorer-mcp`.

```bash
# macOS
open Decentraland.app --args --mcp

# Windows
Decentraland.exe --mcp-port 8124
```

In the Unity Editor, add `--mcp` to `Main Scene Loader → Debug Settings → App Parameters`.

> **`npm run start -- --mcp` does not currently work.** `@dcl/sdk-commands` accepts `--mcp` / `--mcp-port` and forwards them (along with anything after a second standalone `--`) into the `decentraland://` deep link it uses to launch the installed client — see `packages/@dcl/sdk-commands/src/commands/start/explorer-alpha.ts`. Since the allowlist drops both keys, the creator flow is silently a no-op today. Launch the client manually with the flags meanwhile. Fixing it properly most likely means a **loopback-gated** `mcp` entry in `DeepLinkAllowlist`, next to `local-scene` and `skip-auth-screen` — that is a separate change and a product decision (SEC-019/020), and the [Security model](#security-model) explains what it would imply.

## Security model

- The listener binds to **127.0.0.1 only** — it is never reachable from the network.
- Browser-originated requests are rejected unless their `Origin` is localhost (defense against drive-by pages and DNS rebinding). Requests without an `Origin` header (CLI clients) are allowed.
- The server only exists while the process runs with the flag; there is no persistence and no authentication token in v1.
- **The server itself is exempt from the build boundary, on purpose.** Creator Tools drives the *installed* client through it, so the transport and the scene-iteration tools (`screenshot`, `get_scene_state`, `get_scene_logs`, `teleport`, `reload_scene`, …) must ship in release builds. Removing them is not on the table, and none of them can read or write arbitrary client state.
- **The advanced surface is not in release builds at all.** The client-UI automation tools (`list_ui_elements`, `get_ui_state`, `click_ui`, `hover_ui`, `set_ui_text`, `press_key`, `scroll`) and the reflection tools (`get_component_property`, `set_component_property`, `call_static_method`) exist to drive the client's own UI from a test suite; no creator flow uses them. They sit behind the `MCP_TEST_AUTOMATION` compile define — **on** by default, so local dev, QA builds and the automation suite get them with no extra setup, and removed for release by `CloudBuild.PreExport` when `IS_RELEASE_BUILD=true`. In a release build the code is not in the binary, so no runtime flag, argument or protocol message can surface it. `tools/list` differs accordingly.
- **Inside a non-release build, reflection takes a second gate.** `set_component_property` and `call_static_method` are arbitrary in-process mutation and invocation, and `get_component_property` reads arbitrary state. `--mcp` alone does not register them: `FeatureId.McpReflection` resolves to false, so they never enter the registry and are absent from `tools/list` rather than failing when called. `--mcp-reflection` adds them, and is itself inert without `--mcp`.
- **`call_static_method` takes a third gate of its own.** Even registered, it resolves only methods marked `[McpCallable]`; an unmarked method is skipped before its name is compared, so the tool cannot reach the stored identity, the filesystem, process control or any other static the client did not deliberately expose. `get_component_property` / `set_component_property` are bounded differently — by reach rather than by opt-in: they address a component of a resolved client UI element, follow at most four member steps, and refuse any step declared in `DCL.Web3` or `DCL.Prefs`.
- **Why the compile gate, and not the runtime flags alone.** Today every flag is command-line-only, which already implies local shell — and someone with local shell can attach a debugger or swap the binary anyway. But that argument is about the attacker's starting position, not the artifact, and it is about to weaken: fixing the creator flow (above) most likely means allowlisting `mcp` for loopback realms, at which point a `decentraland://` link can start the server on a shipped client. The compile gate is what keeps that change safe to make — it is a property of the release binary that no allowlist edit can undo.
- **Consequence for future changes.** `mcp-reflection` must stay out of `DeepLinkAllowlist` unconditionally. If `mcp` is allowlisted, keep it loopback-gated, and do not weaken the `MCP_TEST_AUTOMATION` boundary in the same change.

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

**`tools/list` differs by build.** 🔧 tools are compiled out of release builds by `MCP_TEST_AUTOMATION`; everything else ships everywhere — see [Security model](#security-model).

### Seeing

| Tool | Arguments | Returns |
|---|---|---|
| `screenshot` | `maxWidth?` (default 1280), `quality?`, `worldOnly?` (exclude UI; post-processing still applied) | Downscaled image of the current view (UI included by default) + caption |
| `get_player_state` | — | Player position/rotation/parcel/velocity/grounded + camera position/rotation/mode + wallet address |
| `get_scene_state` | — | Current parcel, scene name/state (incl. `JavaScriptError`/`EcsError`), readiness, loading stage |
| `get_scene_logs` | `limit?`, `severity?`, `sinceSeq?` | Scene JS console output with monotonic sequence numbers for incremental polling |
| `get_network_log` | `limit?` (default 50), `sinceSeq?`, `failedOnly?`, `status?` | Recent client HTTP activity as `{latestSeq, returned, entries}` — one `{seq, timestamp, url, method, status, mimeType, sizeBytes, durationMs, failed, reason?}` per request (the same data the Chrome DevTools Network domain shows). `latestSeq` is what the next call passes back as `sinceSeq` |
| 🔧 `list_ui_elements` | `nameFilter?` | Live client UI elements (uGUI + UI-Toolkit) as `{path, name, type, system, interactable, visible, text?}` |
| 🔧 `get_ui_state` | `element` (path, name or path expression) | One UI element's current state; an error result means it is not in the hierarchy — that is the absence check |
| 🔧 `get_component_property` | `element`, `component`, `property` | One property read off one component of a UI element (e.g. `GraphicRaycaster.enabled`). Needs `--mcp-reflection` |
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
| 🔧 `click_ui` | `element` (path, name or path expression) | Clicks a real **client UI** control (not a scene entity — see below), hit-tested through the live `EventSystem` |
| 🔧 `hover_ui` | `element` (path, name or path expression) | Moves the pointer onto a control so hover-only state appears, hit-tested like `click_ui`. **uGUI only** — a UI-Toolkit element answers `hovered: false` with the reason |
| 🔧 `set_ui_text` | `element`, `text`, `submit?` (default true) | Types into a uGUI `InputField`, a `TMP_InputField` or a UI-Toolkit `TextField` |
| 🔧 `press_key` | `key`, `eventType?` (`press`\|`down`\|`up`), `seconds?` | Presses a keyboard key on the Input System device the client reads |
| 🔧 `scroll` | `element`, `deltaX?`, `deltaY?` | Sends a mouse-wheel scroll at a UI element. **uGUI only** — a UI-Toolkit element answers `scrolled: false` with the reason |
| 🔧 `set_component_property` | `element`, `component`, `property`, `value` | Writes a property/field on a component of a UI element. Needs `--mcp-reflection` |
| 🔧 `call_static_method` | `type`, `method`, `assembly?`, `parameters?` | Invokes a public static method **marked `[McpCallable]`** in the running client and returns its value; anything unmarked is refused. Needs `--mcp-reflection` |

## Client HTTP activity (`get_network_log`)

`get_network_log` reads a ring buffer of the client's recent HTTP requests, closing the parity gap with the Chrome DevTools **Network** domain. It taps the same seam the DevTools bridge does — the shared `WebRequestController`'s `IWebRequestAnalyticsHandler` pipeline — so its **coverage is identical**: content, realm, asset-bundle, texture, audio, wearable/emote and profile requests all flow through it. Requests made with a raw `UnityWebRequest` or `HttpClient` **outside** the controller are not seen. Each finished request is captured once (at completion or failure) and the buffer holds the most recent 512 (bounded, metadata only). Capture is part of the MCP feature, not of every run: it begins when `McpServerPlugin` is built and nothing is recorded — no lock, no timestamp, no retained URL — in a client started without `--mcp`/`--mcp-port`. Boot traffic that finishes before the plugin exists (feature flags, clock sync) is therefore not in the buffer; from then on it records continuously, whether or not an agent is connected.

Every answer is `{latestSeq, returned, entries}`: `returned` is how many entries this call carried, and `latestSeq` is the sequence number of the newest entry in the buffer (`-1` while it is empty). Feed `latestSeq` back as `sinceSeq` to poll incrementally (like `get_scene_logs`). It is the whole buffer's frontier rather than the last entry returned, which is what makes it right when a filter left the answer empty — there is no last `seq` to advance past, and re-sending `sinceSeq` unchanged would re-examine the same requests forever. It is read before the copy, so it never runs ahead of the entries in the same answer: a request landing between the two is re-delivered on the next poll instead of being skipped. **`limit` truncation is still lossy** — the answer holds the newest `limit` matches, and advancing to `latestSeq` will not bring back the older ones it dropped, so raise `limit` or poll more often rather than expecting to catch up. Each entry also carries its own `seq` and an ISO-8601 `timestamp`. `failedOnly` returns only unsuccessful requests — a transport failure **or** an HTTP status ≥ 400. `status` filters to one exact HTTP status (e.g. `404`).

## Client UI automation

`list_ui_elements`, `get_ui_state`, `click_ui`, `hover_ui`, `set_ui_text`, `press_key` and `scroll` drive the client's **own UI** (menus, buttons, HUD) — not SDK7 scene UI, and not scene entities (`click_entity` is for those). They work by enumerating the live UI object hierarchy, then acting on real controls. The three reflection tools ([below](#reflection-tools---mcp-reflection)) address the same elements but read and write their components directly, and take a second gate.

### Finding elements

Both UI systems the Explorer uses are walked:

- **uGUI** — every active node in the Transform tree under each active **root** `Canvas`, not just controls, so a panel, a container or a label can be located and waited on. A nested (non-root) `Canvas` is reached as a descendant of its root one.
- **UI-Toolkit** — every element under each active `UIDocument`'s `rootVisualElement`.

Those two roots are the whole of the coverage, and the descent stops at the first inactive node — a deactivated GameObject hides its entire subtree. Anything outside that is simply not in the hierarchy, and every tool answers `No UI element matched` for it: a control whose root `Canvas` or `UIDocument` is inactive, or that hangs off an inactive ancestor, cannot be addressed. There is no fallback pass over registered controls to catch it — bring the owning UI up first, exactly as a user would.

`list_ui_elements` sets a `truncated` flag when the cap is hit; `nameFilter` keeps only elements whose name or path contains the substring (case-insensitive), which is how you stay under the cap on a full HUD. Each `path` is system-prefixed (`ugui:/…` or `uitk:/…`) so a later call can re-resolve it against a fresh walk. Same-named siblings carry the `Name[i]` indexer; a unique name stays bare, so a path reads like the Hierarchy window.

`type` is the most telling component on the node — the control, else the label, else the graphic, else `GameObject`. `interactable` is the control's own interactable state, or, for a plain node, whether it carries a raycastable graphic. `visible` is `activeInHierarchy` with the node's graphic (if any) enabled. `text` reads **TextMeshPro** (`TMP_Text`, `TMP_InputField`) before legacy `UnityEngine.UI.Text`, a `Toggle`'s on/off, or the nearest label beneath the node; UI-Toolkit reads a `TextElement`/`Toggle`/`TextField`.

A field that masks its input on screen — a uGUI or TMP input field whose content/input type is `Password` or `Pin`, or a UI-Toolkit `TextField` with `isPasswordField` — reports the fixed string `<masked>` instead of its value. The token is the same whatever the field holds, so neither the value nor its length leaks; an empty password field reads `<masked>` too. This covers the tools that walk the UI (`list_ui_elements`, `get_ui_state`) and `set_ui_text` still writes such a field normally. It is **not** a secrecy boundary for the whole server: `get_component_property` reads `TMP_InputField.text` off the same element directly and is not masked — that tool is bounded by `--mcp-reflection` instead.

### Addressing an element

Every tool that acts takes an `element`, resolved against a fresh walk in this order: an exact `path`, then a **path expression**, then an exact element **name**, then a loose path-suffix/contains match. A path expression is a small path dialect over the same hierarchy. It borrows XPath's separators and wildcard, but **not** its predicates: `Name[i]` is a **zero-based sibling index**, so the first same-named sibling is `[0]` — one lower than the same expression would mean in XPath.

| Syntax | Meaning |
|---|---|
| `//Panel` | a node named `Panel` at any depth |
| `/Root/Panel` | anchored at the hierarchy root |
| `Panel/Button` | no leading separator reads as `//Panel/Button` |
| `//Panel//Button` | `Button` at any depth below `Panel` |
| `//Grid/Item(Clone)[2]` | the same-named sibling at **zero-based** index 2 — the third one |
| `//Panel/*` | any single node |

A query has to consume the whole path, so it identifies the element itself and not one of its ancestors. Attribute predicates (`[@name=…]`, `[@component=…]`), `contains(…)`, `text=` and the `..` parent axis are **not** supported — the suite uses none of them.

Because `get_ui_state` errors when nothing matches, polling it serves as both waits a suite needs: wait-until-present, and wait-until-gone.

### Acting on an element

`click_ui` hit-tests before it dispatches: it raycasts the live `EventSystem` at the element's screen centre and sends pointer down/up/click to whatever is actually on top (via `ExecuteHierarchy`, so the real handler receives it). A hit on the element itself, on one of its descendants (a `Button`'s own `Image`) or on an ancestor drawing behind it all count as the element; anything else is a blocker, and the result is `clicked: false` with it named — a modal over a button no longer produces a green result on a UI a user cannot operate. An element with no raycastable graphic still gets a direct dispatch and says so (`dispatch: "direct"`).

UI-Toolkit controls take a different route: `click_ui` sends them a navigation-submit (what activating a focused `Button`/`Toggle` does) and returns before any hit-test runs. The hit-test fields therefore appear on the uGUI path only — `screenX`/`screenY` on every uGUI result, and `topHit` only where the raycast actually found something, so it is absent on exactly the `direct` dispatch. A UI-Toolkit result carries none of them — only the element's own fields plus `clicked: true` and `dispatch: "uitk-submit"`. `dispatch` is the field to branch on: it is always present.

`hover_ui` moves the pointer onto a control and leaves it there, exiting whatever it was on before, so hover-only state (tooltips, highlights, reveal-on-hover buttons) can be asserted. It drives **uGUI only**: a UI-Toolkit element takes its pointer events from its own panel and answers `hovered: false` with that as the reason. `set_ui_text` writes through the field, so its value-changed notification fires as it does for a real edit; `submit` (default `true`) additionally raises end-edit/submit, which is what pressing Enter does and what the search, OTP and username handlers subscribe to. Unlike hover and scroll, it does cover UI-Toolkit — a `TextField` is written the same way.

`press_key` queues keyboard state onto the Input System device the client reads, which is the only seam that reaches every `DCLInput` action at once. Key names are the Input System's own `Key` members (`Escape`, `Space`, `X`, `F5`, `LeftShift`, `Digit1`), case-insensitive — there is no second spelling to drift out of sync. The real action maps still gate it: a HUD shortcut does nothing while its map is disabled (a modal open, a text field focused), exactly as for a user. `eventType: down` leaves the key held so chords compose; `up` releases it. Because a state event carries the whole keyboard, a key a human is physically holding is released as a side effect.

`scroll` is **uGUI only** as well — a UI-Toolkit element scrolls through its own `ScrollView` and answers `scrolled: false` with that reason. On a uGUI element it is hit-tested like `click_ui` and dispatched at the real top hit, so the wheel reaches the enclosing `ScrollRect`; unlike `click_ui` there is no direct-dispatch fallback: a wheel notification travels up from the raycast hit, so an element with nothing raycastable at its centre yields `scrolled: false` with `dispatch: "none"` — nothing was sent at all — rather than a wheel event nobody handles.

### Reflection tools (`--mcp-reflection`)

These three need both gates — absent from release builds, and unregistered (not merely refusing calls) without `--mcp-reflection` even where present. Read the [Security model](#security-model) before changing either gate.

`get_component_property` reads a dotted property path off a named component of the element (`GraphicRaycaster` + `enabled`, a view's `IsLoading`); polling it reproduces `WaitForComponentProperty`. `set_component_property` writes one back, converting strings, booleans, numbers and enums; a member with no setter, or one reached through a struct read by value (where the write would be lost), is refused rather than silently dropped. `call_static_method` invokes a public static method by full type name, optional assembly and positional parameters — the escape hatch for a purpose-built in-client test hook. It reaches **only** methods the client opted in with `[McpCallable]` (`Core/McpCallableAttribute.cs`): resolution skips an unmarked method before it compares the name, so every other static in the process — the stored identity, the filesystem, process control — is refused with the same message an absent method gets. **No method carries the attribute today**, so the tool resolves nothing until someone marks a hook. The attribute lives in `DCL.McpServer`, so a hook must sit in an assembly allowed to reference it — not in one `DCL.McpServer` itself references, which would be a cycle. A hook needs no incoming reference: it is found reflectively.

> Prefer driving the real UI: a write bypasses whatever invariants the owning code maintains, and a green test on a forced state proves less than one on a state the UI reached by itself.

> Paths are a single-frame snapshot: list, then act promptly. The walk, the hit-test and the dispatch all touch Unity UI objects and run on the MCP main-thread hop.

### What a UI suite can do with this

This is the reference for whether the embedded MCP server is enough to drive [`decentraland/explorer-automation`](https://github.com/decentraland/explorer-automation) without an instrumented build. The catalog above is keyed by tool; this is keyed by what a suite needs, which is the question a migration actually asks.

| Capability a UI suite needs | MCP tool |
|---|---|
| Find an element, or wait for one to appear | `get_ui_state`, polled — by path, name or path expression |
| Wait for an element to disappear | `get_ui_state` — an error result *is* the absence |
| Enumerate everything on screen | `list_ui_elements` |
| Click a control | `click_ui` — hit-tested, so a covered control fails instead of falsely passing (a UI-Toolkit control gets a navigation-submit instead) |
| Hover a control to reveal tooltips or hover-only state | `hover_ui` — same hit-test; exits the previous element first. **uGUI only** |
| Read a label, field value or toggle state | `text` on `get_ui_state` / `list_ui_elements` (TextMeshPro included) |
| Type into a field | `set_ui_text` |
| Press a key | `press_key` |
| Scroll a list | `scroll` — **uGUI only** |
| Read component state a view exposes but the UI does not | `get_component_property`, polled — needs `--mcp-reflection` |
| Force a view into a state instead of choreographing it | `set_component_property` — needs `--mcp-reflection` |
| Call a purpose-built in-client test hook | `call_static_method` — needs `--mcp-reflection`, and the hook must be marked `[McpCallable]` |
| Capture the screen for a visual baseline | `screenshot` |
| Know when the scene is ready | `get_scene_state` — `isReady`, `assetsLoadingConcluded`, `loadingScreenOn`, `loadingStage` |
| Read client logs and HTTP activity | `get_scene_logs` (scene console) + `get_network_log` |

Deliberately absent, for want of a consumer:

- **Calling an instance method on a component** — no call site in the suite; `call_static_method` covers the hook pattern it actually uses.
- **Addressing an element by instance id, or walking up to its parent** — deliberately not provided, and not an oversight to "fix". The one fixture that uses it does so *because* it could not locate elements by name; the full-hierarchy walk and path expressions above remove that need, and the fixture is marked for deletion. Adding an id scheme would preserve a workaround for a defect this surface fixes, and leave two ways to address an element where one is enough.
- **Touch, swipe, drag, cursor warping, scene loading, time scaling, PlayerPrefs** — no call site in the suite.

The static scene-readiness probe under `Explorer/Assets/DCL/Infrastructure/SceneRunner/Scene/` (hooked from `SceneFacade`) is now redundant with `get_scene_state`, which reports a strict superset of it. It is **still in use** by the visual suite, so it stays until that suite migrates. `call_static_method` cannot poll it as things stand — the probe carries no `[McpCallable]`, and marking it would mean applying an attribute from `DCL.McpServer` inside an assembly `DCL.McpServer` already references, which is a cycle. Use `get_scene_state` instead; it reports the superset.

The suite drives a non-release build (any build that keeps `MCP_TEST_AUTOMATION`, which is the default) and needs all three reflection tools, so its runner passes both flags — no instrumented build is involved:

```bash
Decentraland.exe --mcp --mcp-reflection --windowed-mode --resolution 1280x720
```

Against a release build the UI-automation and reflection tools are not in `tools/list`, and only the scene-iteration half of the catalog answers.

## Structured output

`get_player_state`, `get_scene_state` and `list_scene_entities` also return `structuredContent` mirroring their text payload and declare a matching `outputSchema` in `tools/list` (MCP 2025-06-18). This is done **only as an example on the read-only state tools that benefit from it now** — every other tool returns text content only. A tool opts in by overriding `McpTool.OutputSchema` (default `null`); the same `McpJsonSchema` builder produces the schema.

## The scene-iteration loop

1. Serve the scene: `npm run start -- --no-client` in the scene folder (serves at `http://127.0.0.1:8000` and hot-reloads on file changes; it launches no client, because the MCP flag has to reach the client's own command line — `npm run start -- --mcp` looks like it does the same thing in one step but is a silent no-op today, see the note under [Enabling](#enabling)).
2. Launch the Explorer against it with the server on — the installed client, a local build, or the Editor:

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

- `Explorer/Assets/DCL/McpServer/` — feature root, its own `DCL.McpServer` assembly. Three folders are folded into other assemblies via `.asmref` so they can reach code that assembly doesn't reference:
  - `Core/` — protocol, transport and tool contract: `McpHttpServer` (`HttpListener` server + Origin validation), `McpJsonRpcDispatcher` (JSON-RPC 2.0 routing; `PROTOCOL_VERSION` `2025-06-18`), `McpTool` (abstract tool base), `McpToolsRegistry`, `McpToolResult`, `McpToolAnnotations` (behaviour hints), `McpJsonSchema` (typed schema builder), `McpEcsRequest`, `McpWireEnum`, and `McpCallableAttribute` — the opt-in marker `call_static_method` requires, behind `MCP_TEST_AUTOMATION` like the tool itself.
  - `Tools/` — one class per tool: 17 that ship everywhere, plus 10 behind `MCP_TEST_AUTOMATION` (of which 3 also need `--mcp-reflection`).
  - `Components/` — ECS components for the input-driving tools: `McpMovementOverride`, `McpPointerEventIntent`.
  - `Systems/` — **folded into `DCL.Plugins`** via `.asmref`: `McpServerPlugin` (builds the registry and hosts the server in `InjectToWorld`), `McpInputOverrideSystem` (held movement), `McpPointerEventSystem` (synthetic pointer press/release delivery; `ClickEntityTool` composes a click from two intents).
  - `Utils/` — `SceneLogBuffer`, `JObjectExtensions`, `UiAutomation` (the uGUI + UI-Toolkit hierarchy walk, the `EventSystem` hit-test and the pointer/text/wheel dispatch), `McpKeyboardInput` (key-name parsing + Input System key events), `ComponentProperty` (component lookup and reflected reads/writes) and the pure `UiElementPath` (path building/matching).
  - `Tests/` — EditMode tests **folded into `DCL.EditMode.Tests`** via `.asmref`: dispatcher / registry / result routing, the HTTP server, the input schema, the read-only state tools, the pointer-click system, the ECS-request helper, wire-enum mapping, the network-log ring buffer, UI-element path rules, key-name parsing and held-key tracking, component lookup and component-property reads/writes, and `call_static_method`'s resolve/bind/invoke path including the `[McpCallable]` refusal.
  - `Tests/PlayMode/` — PlayMode tests **folded into `DCL.PlayMode.Tests`** via a second `.asmref`, because the hit-test needs a live `EventSystem` and a real canvas: `click_ui` and `hover_ui` each land on a clear control and are refused on an occluded one, and a hover exits the element it was previously on. They drive `UiAutomation` directly rather than over HTTP — the transport already has its own coverage, and binding a port makes a CI agent flaky. Against the real client HUD the walk and dispatch stay integration-only.
- Network capture: `get_network_log` reads a `McpNetworkLogBuffer` fed by an `McpNetworkAnalyticsHandler` (an `IWebRequestAnalyticsHandler`), both in `Explorer/Assets/DCL/WebRequests/Analytics/` (`DCL.Network` assembly). `WebRequestsContainer` always constructs the handler and adds it to the `WebRequestsAnalyticsContainer` alongside the Chrome DevTools handler, but leaves it dormant and `NetworkLogBuffer` null. Inside its `FeatureId.McpServer` gate, `DynamicWorldContainer` calls `WebRequestsContainer.EnableMcpNetworkLog()` — which creates the buffer and starts the handler recording — then passes the buffer to `McpServerPlugin`, which registers `GetNetworkLogTool` only when it is non-null. The feature registry is initialized after the container is built, which is why enabling is a second step rather than a `CreateAsync` argument.
- Build gating: the `MCP_TEST_AUTOMATION` define wraps the ten advanced tools, the four `Utils` types they need (`UiAutomation`, `UiElementPath`, `McpKeyboardInput`, `ComponentProperty`), `Core/McpCallableAttribute`, their EditMode and PlayMode tests, and their registration block in `McpServerPlugin` (including the `McpKeyboardInput.Reset()` its `Dispose` makes to drop any key left held). It is set in `ProjectSettings` (Standalone) and removed for release by `CloudBuild.PreExport` → `RemoveScriptingDefineSymbol`.
- Runtime gating: `FeatureId.McpServer` in `FeaturesRegistry` (resolved as `appArgs.HasFlag(MCP) || appArgs.HasFlag(MCP_PORT)`); `DynamicWorldContainer.CreateAsync` reads `FeaturesRegistry.Instance.IsEnabled(FeatureId.McpServer)` and adds `McpServerPlugin`. `FeatureId.McpReflection` (`McpServer && appArgs.HasFlag(MCP_REFLECTION)`) is read inside the gated block and adds the three reflection tools.
- Flags: `AppArgsFlags.MCP` / `MCP_PORT` / `MCP_REFLECTION`; log category: `ReportCategory.MCP`.
