# Feature context — Scene-triggered Explorer UI (`openExplorerUi`)

> NOTE: This file was reconstructed after the original working copy was lost. It
> captures the authoritative domain language for the feature; please review and
> refine the wording if the original had additional nuance.

## What the feature is

A restricted action `openExplorerUi` that lets an SDK7 scene ask the Explorer to
open a native fullscreen panel (Map / Settings / Backpack / CameraReel /
Communities / Places / Events) via `~system/RestrictedActions`, returning a typed
verdict. Iteration 1 = open + verdict only (no event channel, no params).

## Domain language (authoritative — use these terms)

- **open result / verdict** — the outcome of an `openExplorerUi` call is an
  **open result verdict**, modeled as the enum `OpenExplorerUiResult`, never a
  `bool success`. New outcomes are expressed by adding enum values, not booleans.
- **`OpenExplorerUiResult`** values (protocol bare names; C# members are the
  PascalCase forms; host-JS runtime keys mirror the bare names):
  - `UNSPECIFIED` (0) — default / unset.
  - `OPENED` (1) — the panel was opened.
  - `REJECTED_NOT_CURRENT_SCENE` (2) — the standard restricted-actions
    current-scene gate rejected the call.
  - `REJECTED_ALREADY_OPEN` (3) — a fullscreen panel is already open (also covers
    repeat / rapid re-invocation). Section-switching on an already-open panel is
    out of scope for now.
  - `REJECTED_FEATURE_DISABLED` (4) — the requested section is hidden by feature
    flags, or the requested `ExplorerUi` value is unsupported.
  - `REJECTED_NO_USER_GESTURE` (5) — the call did not originate from a user
    gesture (see gesture rule below).
- **`ExplorerUi`** (`EU_*`) — identifies which fullscreen panel to target.
- **gesture rule ("once per frame")** — a call is honored only if a user pointer
  input happened in the current or immediately preceding scene tick. The window
  is a hardcoded constant, not a feature flag.

## Layering (runtime data flow)

```
scene src/index.ts  →  ~system/RestrictedActions (host module, provided by Explorer)
  →  Explorer host JS  StreamingAssets/Js/Modules/RestrictedActions.js
  →  C# RestrictedActionsAPIWrapper.OpenExplorerUi(int)
  →  RestrictedActionsAPIImplementation.TryOpenExplorerUi(int) → gates → verdict
```

`~system/*` are HOST modules (runtime values provided by the Explorer), so the
host JS must export the enum *values*, not just the `.d.ts` types.
