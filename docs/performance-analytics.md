# Performance Analytics (`performance_report`)

The client periodically emits a `performance_report` analytics event (via Segment) describing CPU/GPU frame timing, hiccups, and memory. It is produced by [`PerformanceAnalyticsSystem`](../Explorer/Assets/DCL/PerformanceAndDiagnostics/Analytics/Systems/PerformanceAnalyticsSystem.cs) from data collected by [`Profiler`](../Explorer/Assets/DCL/PerformanceAndDiagnostics/Profiling/Profiler.cs).

> This is runtime telemetry sent from real sessions. It is **not** the local [Performance Benchmark](performance-benchmark.md) PDF tool, and it is separate from [Diagnostics](diagnostics.md) (ReportHub/Sentry logging).

## Reporting pipeline

- `PerformanceAnalyticsSystem` runs in the `InitializationSystemGroup` (after `DebugViewProfilingSystem`).
- The event fires every `PerformanceReportInterval` seconds of **played** time. The shipped `AnalyticsConfiguration.asset` uses **15 s**; the code default and the Playground asset use 1 s.
- Collection is **paused** whenever the realm is not configured, the loading stage is not `Completed`, or the app is unfocused. Loading screens and backgrounded time are intentionally excluded.

## Data source

Both frame-time families read from Unity `ProfilerRecorder` ring buffers (near-zero overhead, consistent with the Unity Profiler):

- **Main thread** (`ProfilerCategory.Internal`, "Main Thread"): CPU main-thread time. This is *not* full presented-frame time and *not* GPU time. Feeds the `hiccups_*` and `*_frame_time` fields.
- **GPU** (`ProfilerCategory.Render`, "GPU Frame Time"): feeds the `gpu_*` fields.

The buffer holds up to 1024 frames (`FRAME_BUFFER_SIZE`).

## What a hiccup is

A hiccup is any single frame whose sampled thread time exceeds the **effective threshold**:

```
threshold = max(50 ms, HICCUP_TARGET_FRAME_TIME_MULTIPLIER x targetFrameTime)
```

with `HICCUP_TARGET_FRAME_TIME_MULTIPLIER = 2` and the target frame time derived from `Application.targetFrameRate`.

- `targetFrameRate <= 0` (vsync or uncapped, which run at >= 60 FPS in practice) → the target-relative bar is below the floor, so the threshold stays at the **50 ms floor**.
- Explicitly capped configs raise the bar so their normal cadence is not counted as hiccups.

| Target | 2x target | Applied threshold (50 ms floor) |
|---|---|---|
| 120 / 90 / 60 fps | <= 33 ms | **50 ms** |
| 40 fps | 50 ms | **50 ms** |
| 30 fps | 67 ms | **67 ms** |

The threshold applied to each report is emitted as `hiccups_threshold`, so counts can be interpreted and compared across cohorts. The same threshold is used for CPU and GPU hiccups.

## Measurement window

Each report describes exactly its own `PerformanceReportInterval`. After every report (and whenever collection stops on a teleport/unfocus), both the custom frame-time buffers and the hiccup `ProfilerRecorder`s are reset. `ProfilerRecorder` has no public flush, so the recorders are disposed and recreated (see `Profiler.ResetHiccupRecorders`). Consequences:

- No frame is counted in more than one report.
- Stale pre-transition frames are not attributed to the next realm/scene.
- The on-screen debug HUD reads the same recorders, so its hiccup counter reflects the current (post-reset) window rather than a trailing 1024 frames.

Because the window can be shorter than 1024 frames (warm-up, or low framerate), `hiccups_samples_amount` reports the actual frame count so consumers can normalize (e.g. per-1000-frames or per-second) themselves.

## Emitted fields

Main-thread hiccups (all times in ms):

| Field | Meaning |
|---|---|
| `hiccups_in_thousand_frames` | Count of hiccup frames in the window (not normalized to 1000; see `hiccups_samples_amount`). |
| `hiccups_time` | Sum of the **full** duration of each hiccup frame. |
| `hiccups_excess_time` | Sum of each hiccup frame's **excess over the threshold** ("time lost" beyond the bar). |
| `hiccups_min` / `hiccups_max` / `hiccups_avg` | Duration stats of hiccup frames. |
| `hiccups_samples_amount` | Frames the window was measured over. |
| `hiccups_threshold` | The threshold actually applied this report. |

Frame-time family: `min_frame_time`, `max_frame_time`, `mean_frame_time`, `frame_time_percentile_{5,10,20,50,75,80,90,95}`, `samples`, `samples_amount`.

GPU family: the same fields with a `gpu_` prefix (including `gpu_hiccups_excess_time` and `gpu_hiccups_samples_amount`). GPU hiccups use the same threshold as the main thread, so there is no separate `gpu_hiccups_threshold`.

Plus quality/memory context: `quality_level`, `player_count`, `total_time`, JS heap (`jsheap_*`, `running_v8_engines`), and process memory (`total_used_memory`, `system_used_memory`, `gc_used_memory`, `total_gc_alloc`).

## Interpretation notes

- `hiccups_in_thousand_frames` is a **count**, not a rate. Normalize with `hiccups_samples_amount` before comparing windows or cohorts.
- `hiccups_time` includes the whole hiccup frame; use `hiccups_excess_time` for "time lost to hiccups".
- Because the threshold scales with the target frame rate, hiccup counts on intentionally capped configs are lower than a fixed 50 ms bar would produce. This is deliberate: it measures stalls relative to each config's expected cadence. Use `hiccups_threshold` when comparing across cohorts.
- `hiccups_max` remains a good "did the client freeze" signal.
