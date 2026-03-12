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
    "override": true,
    "debugLogMatrix": [
        { "category": "VOICE_CHAT", "severity": "Warning" }
    ],
    "sentryMatrix": [
        { "category": "VOICE_CHAT", "severity": "Exception" }
    ]
}
```

- `"override": true` — Only use file values (replaces entire matrix)
- `"override": false` — Merge with existing matrix values

### Use Cases

- QA-specific log configs for targeted testing
- Production debugging without rebuilding
- Clean log files focused on specific features

## Sentry Configuration

- **Dev branches:** Disabled by default
- **Enable manually:** Run `unity-build` workflow with Sentry enabled
- **Environment:** Configured per build pipeline stage
- Sentry handler respects the CategorySeverityMatrix — only sends entries where Sentry column is enabled

## Code Example — Exception Handling with Logging

From `MinimapController.cs`:

```csharp
private async UniTask<PlacesData.PlaceInfo?> GetPlaceInfoAsync(
    Vector2Int parcelPosition, CancellationToken ct)
{
    try
    {
        return await placesAPIService.GetPlaceAsync(parcelPosition, ct);
    }
    catch (OperationCanceledException _) { }
    catch (NotAPlaceException e)
    {
        ReportHub.LogWarning(ReportCategory.UNSPECIFIED,
            $"Not a place requested: {e.Message}");
    }
    catch (Exception exception)
    {
        ReportHub.LogException(exception, ReportCategory.GENERIC_WEB_REQUEST);
    }

    return null;
}
```
