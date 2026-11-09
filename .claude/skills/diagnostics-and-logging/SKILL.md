---
name: diagnostics-and-logging
description: "Diagnostics, logging, and error reporting via ReportHub. Use when adding log statements, configuring severity matrices, tagging systems with LogCategory, integrating with Sentry, or overriding log levels at runtime."
user-invocable: false
---

# Diagnostics & Logging

## Sources

- `docs/diagnostics.md` — Logging, error handling, and reporting infrastructure
- `docs/override-debug-log-matrix.md` — Runtime log severity override mechanisms

---

## ReportHub API

**Replace all `Debug.Log` calls with `ReportHub`.** ReportHub is a custom `ILogHandler` that routes logs through multiple handlers.

```csharp
// Log a message with category
ReportHub.Log(ReportCategory.SDK_AUDIO_SOURCES, "Audio source initialized");

// Log a warning
ReportHub.LogWarning(ReportCategory.UNSPECIFIED, $"Not a place: {message}");

// Log an exception
ReportHub.LogException(exception, ReportCategory.GENERIC_WEB_REQUEST);
```

### ReportData

Each log entry carries:
- `ReportCategory` — The feature area (e.g., `SDK_AUDIO_SOURCES`, `AUTHENTICATION`)
- `SceneShortInfo` — Optional scene context

### Report Handlers

- **DebugLog** — Colored console output with category prefix
- **Sentry** — Remote error tracking (disabled on dev branches by default)

## System Integration

### LogCategory Attribute

Tag systems with `[LogCategory]` to automatically associate logs with a category:

```csharp
[UpdateInGroup(typeof(CleanUpGroup))]
[LogCategory(ReportCategory.SDK_AUDIO_SOURCES)]
[ThrottlingEnabled]
public partial class CleanUpAudioSourceSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
{
    // All logs from this system are tagged with SDK_AUDIO_SOURCES
}
```

### GetReportCategory

Systems can retrieve their category via `GetReportCategory()` — the value is cached after first call.

### New Features

When creating a new feature, introduce a new `ReportCategory` entry. Do not reuse existing categories for unrelated features.

## CategorySeverityMatrix

Controls which logs get logged per handler, by severity and category. Stored in `ReportsHandlingSettings` ScriptableObject.

Configuration is a matrix of:
- **Rows:** `ReportCategory` values
- **Columns:** Severity levels (Log, Warning, Error, Exception)
- **Values:** Enabled/disabled per handler (DebugLog, Sentry)

## Exception Tolerance

The `ISystemGroupExceptionHandler` (`SceneExceptionsHandler`) tracks exceptions per minute:

- Constant: `ECS_EXCEPTIONS_PER_MINUTE_TOLERANCE`
- If exceeded → `SceneExecutionException` → scene suspended or disposed
- Prevents a single broken scene from crashing the application

## Runtime Overrides

### Method 1 — Chat Command

```
/log-matrix enable VOICE_CHAT Error
/log-matrix disable SDK_AUDIO_SOURCES Warning
```

Changes are lost on client close.

### Method 2 — JSON File

Drop a `.json` file in the build root folder, launch with `--use-log-matrix "filename.json"`:

```json
{
    "isOverride": true,
    "debugLogMatrix": [
        { "category": "VOICE_CHAT", "severity": "Warning" }
    ],
    "sentryMatrix": [
        { "category": "VOICE_CHAT", "severity": "Exception" }
    ]
}
```

Keys must match `CategorySeverityMatrixDto` field names — `JsonUtility` ignores unknown ones. `{ "allOverride": true }` enables every category at every severity in the log file only, and takes precedence over `isOverride`/`debugLogMatrix`. A log captured under `allOverride` contains resolved media stream URLs with signed `sig`/`expire` query params — do not attach it to a public issue.

- `"isOverride": true` — Only use file values (replaces entire matrix)
- `"isOverride": false` — Merge with existing matrix values

### Use Cases

- QA-specific log configs for targeted testing
- Production debugging without rebuilding
- Clean log files focused on specific features

## Sentry Configuration

- **Dev branches:** Disabled by default
- **Enable manually:** Run `unity-build` workflow with Sentry enabled
- **Environment:** Configured per build pipeline stage
- Sentry handler respects the CategorySeverityMatrix — only sends entries where Sentry column is enabled

### Filtering Un-actionable Sentry Events

Some reports are the expected outcome of a condition the client cannot act on: a websocket the remote end dropped (every consumer already retries), a cleartext `http://` URL the player refuses (always scene-authored), Unity's own "Platform does not support compute shaders" on below-spec GPUs (the minimum-specs guard already detects it), or failing requests to a creator's preview server in local scene development. "Not a client defect" is not a reason to leave them in Sentry: the fix is to stop reporting them.

- **Filter at the gateway, not at the call site.** Many of these events are captured by the Sentry Unity SDK's own integrations and never pass through `ReportHub` or the caller's `catch`, so a per-call-site fix only covers one path and the same message returns from the next one. `SentryReportHandler.BeforeSend` is the one place every event passes through: add the case there, and it demotes the event to a breadcrumb (`Demote`) so it still shows up in the trail of the next real report.
- **Match narrowly.** Match by exception type, message prefix or HRESULT, plus a flag such as `isLocalSceneDevelopment` when the event is noise only in one mode. Never by category or a broad base type alone. Wrapped exceptions are already handled: `IsIgnoredException` recurses into `InnerException` and drops an `AggregateException` only when every inner exception is ignored.
- **Name the issues.** Each constant or `case` carries a one-line comment with the reason and the issue numbers it closes, like the existing entries.
- **Pin it with tests** in `SentryReportHandlerShould`: one test that drops the event, and one that keeps a near miss (same type, different message) reported.
- **Do not filter real defects.** Native crashes and engine-internal exceptions with no managed frames can still be real bugs. They stay reported until a root cause shows they are environmental.

## Code Example — Exception Handling with Logging

For a real-world example combining exception handling with ReportHub logging, see the **async-programming** skill's `MinimapController.GetPlaceInfoAsync` pattern. It demonstrates the standard three-tier catch hierarchy: silent `OperationCanceledException`, `LogWarning` for expected errors, and `LogException` for unexpected failures.
