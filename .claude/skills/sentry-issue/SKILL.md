---
name: sentry-issue
description: >
  Investigate Sentry issues for the Decentraland Unity Explorer project.
  Trigger whenever a Sentry short ID is mentioned (e.g. UNITY-EXPLORER-M94, WEARABLE-PREVIEW-K3,
  "look at sentry issue M94", "can you check UNITY-EXPLORER-AB1") OR when a raw exception
  callstack is pasted. Fetches the issue and full stacktrace directly from Sentry, locates
  the relevant source files, identifies the root cause, provides reproduction steps, suggests
  fixes grounded in the project's patterns, and offers to set up a fix branch. Use this skill
  even if the user just pastes an issue ID without asking a specific question.
user-invocable: true
---

# Sentry Issue Investigator

## Step 0: Check Setup

Before anything else, look for `.env` in the repo root and check that `SENTRY_AUTH_TOKEN` is set.

```bash
# Load the token
SENTRY_AUTH_TOKEN=$(grep -m1 'SENTRY_AUTH_TOKEN' .env 2>/dev/null | cut -d= -f2)
```

If `.env` is missing or `SENTRY_AUTH_TOKEN` is empty, tell the user:

> **Setup required**: Create a `.env` file in the repo root with:
> ```
> SENTRY_AUTH_TOKEN=your_token_here
> ```
> To get a read-only token: go to **https://decentraland.sentry.io/settings/account/api/auth-tokens/** (or ask your team lead for a token with `issue:read` scope). Then re-paste the issue ID and I'll pick up from here.

If a raw callstack was pasted instead of an ID, skip to Step 2 using the pasted stack directly.

---

## Step 1: Fetch the Issue from Sentry

Use two API calls to get the full issue and its latest event.

**Call 1 — resolve the short ID to an issue:**
```bash
SENTRY_AUTH_TOKEN=$(grep -m1 'SENTRY_AUTH_TOKEN' .env | cut -d= -f2)
curl -s -H "Authorization: Bearer $SENTRY_AUTH_TOKEN" \
  "https://sentry.io/api/0/organizations/decentraland/issues/?shortId=UNITY-EXPLORER-M94"
```

From the response, extract:
- `id` — internal issue ID (needed for the next call)
- `shortId` — confirm it matches
- `title` — exception type and message
- `culprit` — the method where it was thrown
- `level`, `status`, `firstSeen`, `lastSeen`, `count` — for context
- `permalink` — link back to Sentry

**Call 2 — fetch the latest event with full stacktrace:**
```bash
curl -s -H "Authorization: Bearer $SENTRY_AUTH_TOKEN" \
  "https://sentry.io/api/0/organizations/decentraland/issues/{id}/events/latest/"
```

From the response, look in `entries` for the entry with `type: "exception"`. Inside `data.values`, each exception has:
- `type` — exception class name
- `value` — exception message
- `stacktrace.frames` — array of stack frames

Filter frames to project-owned code: keep frames where the `absPath` or `filename` contains `Explorer/Assets/DCL` or `in_app: true`. Skip Unity engine internals, il2cpp generated files, and third-party packages.

If the short ID returns an empty array or an error, tell the user the ID wasn't found and ask them to double-check it in Sentry.

---

## Step 2: Parse the Callstack

Whether from Sentry or a pasted stack, extract:
- The **crash site** — the topmost project-owned frame (deepest in the call chain, closest to the throw)
- The **call chain** — what called what, ignoring engine/infrastructure frames
- The **exception type** — e.g. `NullReferenceException`, `InvalidOperationException`, `ScriptEngineException`
- The **exception message** — often contains the most useful signal

Map the stack frames back to local source files using the path components (e.g. `DCL/Events/EventsCalendarController.cs`).

---

## Step 3: Locate and Read Source Files

For each project-owned frame:
1. Use `Glob` to find the file under the repo root: `**/<FileName>.cs`
2. Read ±40 lines around the referenced line number
3. If the crash involves a component, controller, or service that references other types (the null object's type, its owner, its factory), read those too — but stay targeted

Look specifically at:
- What object is being accessed at the crash line
- Where that object is created and who owns its lifetime
- Whether the crash is inside an `async` state machine (`MoveNext` in the stack = async)
- Whether it involves ECS components, Unity MonoBehaviours, pooled objects, or `AssetPromise`

---

## Step 4: Pattern Recognition

Match the crash against known patterns in this codebase. Multiple may apply.

### A. UniTask / Async Object Lifetime
An `async UniTask` resumed after the owning controller or component was disposed. The referenced field is now null or points to a destroyed Unity Object.

Signs: `MoveNext` in stack frames, controller field accessed after `await`, no `ct.IsCancellationRequested` check after an `await` wrapped in `SuppressToResultAsync`.

### B. SuppressToResultAsync Swallows Cancellation
`SuppressToResultAsync` catches `OperationCanceledException` and returns a failed `Result` — but the calling code doesn't check `ct.IsCancellationRequested` afterwards, so it continues running on a disposed/deactivated object.

Signs: `SuppressToResultAsync` in the stack, missing `if (ct.IsCancellationRequested) return;` after it.

### C. ECS Ref Invalidation
A `ref` component reference was held across a structural change (Add/Remove component, archetype move), invalidating the pointer.

Signs: crash inside a query callback, structural changes nearby in the same method.

### D. Component Not Present / Entity Deleted
A component was accessed without checking for `DeleteEntityIntention` or using `TryGet`.

Signs: direct `World.Get<T>()` without guard, entity in mid-deletion.

### E. Unity MonoBehaviour Lifecycle
A MonoBehaviour's `Awake()` hadn't been called yet (parent GameObject was inactive), or was called after `Destroy()`. Fields initialized in `Awake()` are null if `Awake()` never ran.

Signs: field initialized in `Awake()`, component on a GameObject that starts inactive or is activated while its parent is inactive.

### F. AssetPromise Accessed Incorrectly
`.Asset` accessed before the promise resolved, or the promise was copied by value (losing `ref` state).

Signs: `AssetPromise<T>` in the type chain, `.LifeCycle` not checked.

### G. CancellationTokenSource Not Cancelled on Dispose
A detached `UniTaskVoid` was started with `.Forget()` but the CTS was not cancelled in `Dispose()`, so the callback fires after the owning object is gone.

Signs: `Forget()` call, missing `SafeCancelAndDispose()` in `Dispose()`.

### H. Pool Object Reuse
An object was used after being returned to its pool and acquired by another consumer.

Signs: pooled type in the stack, missing `isRented`/`isActive` guard.

### I. Script Engine / JavaScript Exception Propagating into C#
A JavaScript scene script threw and the exception bubbled through `ClearScript` into the Unity runtime. The C# code that invoked the script didn't handle the `ScriptEngineException`.

Signs: `Microsoft.ClearScript.ScriptEngineException` as the exception type, JS stack trace inside the `value` field.

---

## Step 5: Structured Report

Produce these sections in order.

---

### Issue Overview

| Field | Value |
|---|---|
| Short ID | UNITY-EXPLORER-M94 |
| Title | The exception title from Sentry |
| Culprit | Method where it was thrown |
| First seen | Date |
| Last seen | Date |
| Occurrences | Count |
| Status | unresolved / resolved |
| Sentry link | permalink URL |

---

### Reproduction Steps

Concrete, ordered steps to trigger the crash. Be specific about which scene to load, which UI panel to open, what action to perform, and any timing or ordering that matters.

If reproduction is **straightforward**, just list the steps.

If it is **timing-sensitive or environment-dependent** (race condition, async interleaving, first-time state), say so clearly and move to the next section.

---

### Making It Easier to Reproduce *(only for hard-to-reproduce crashes)*

If the crash requires a race condition or rare state, suggest **instrumentation-only code changes** that surface it more reliably. Rules:
- Only add logging, assertions, or early guards — do not restructure logic or null out variables to force the crash
- Use `ReportHub.LogWarning` / `ReportHub.LogException` rather than `Debug.Log`
- Use `#if UNITY_EDITOR` or `[Conditional("UNITY_EDITOR")]` for assertions not intended for production

---

### Potential Causes

List 2–4 specific, ranked candidates. For each:

**Candidate N: [short label]**
- **What**: the specific object/field/reference that is null or invalid
- **Why**: the lifecycle condition or race that leads to this state
- **Evidence**: line numbers or patterns from the code you read

Ground every candidate in something you observed in the source — no generic speculation.

---

### Suggested Fixes

For each cause, a concrete fix anchored in the project's patterns:

- Missing `ct.IsCancellationRequested` after `SuppressToResultAsync` → add the guard (see CLAUDE.md §9, async-programming skill)
- ECS ref invalidation → complete all ref reads/writes before structural changes (CLAUDE.md §5)
- `Awake()` not yet called → ensure parent hierarchy is active before calling methods, or use lazy init
- `AssetPromise` → always use `ref`, check `.LifeCycle` before `.Asset`
- CTS not cancelled on dispose → add `SafeCancelAndDispose()` in `Dispose()`
- Script engine exception → wrap `ClearScript` invocation in try/catch for `ScriptEngineException`

Keep code snippets minimal — show the pattern, not a full rewrite.

---

## Step 6: Branch Setup

After the report, ask:

> Would you like me to create a fix branch for this issue? I'll check out `dev`, pull latest, and create `fix/sentry-UNITY-EXPLORER-M94`.

If the user says yes:
```bash
git checkout dev && git pull origin dev && git checkout -b fix/sentry-UNITY-EXPLORER-M94
```

Then tell the user:

> **Sentry tip**: When you commit your fix, include `UNITY-EXPLORER-M94` anywhere in the commit message. Sentry will automatically mark this issue as resolved when the next release containing that commit is deployed.

If the user is already on the correct fix branch (detected via `git branch --show-current`), skip the checkout and just surface the Sentry tip.
