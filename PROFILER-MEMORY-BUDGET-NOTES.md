# Profiler memory vs. memory budget — handoff notes

Branch: `fix/exclude-profiler-memory-from-memory-budget` (based on `dev`)

## Problem

`MemoryBudget.GetMemoryUsageStatus()` (`Explorer/Assets/DCL/PerformanceAndDiagnostics/Optimization/PerformanceBudgeting/Memory/MemoryBudget.cs`)
derives the budget status (`ABUNDANCE` / `NORMAL` / `WARNING` / `FULL`) from
`Profiler.SystemUsedMemoryInBytes`, backed by Unity's **"System Used Memory"**
counter — the entire process footprint as seen by the OS.

In the Editor, the Unity Profiler's own sample buffers grow continuously while
it records (easily hundreds of MB). That overhead counts as "used memory", so
merely profiling pushes the budget into `WARNING`/`FULL`, which triggers cache
unloading (`ReleaseMemorySystem`) and starves deferred loading
(`GlobalDeferredLoadingSystem`) — making Editor testing unrepresentative.

## Fix applied

In `Explorer/Assets/DCL/PerformanceAndDiagnostics/Profiling/Profiler.cs`:

- Added a `ProfilerRecorder` for Unity's built-in **"Profiler Used Memory"**
  counter, which measures exactly the memory consumed by the Profiler itself.
- Subtracted it (clamped at zero) from both `SystemUsedMemoryInBytes` and
  `TotalUsedMemoryInBytes`.

`Profiler` is the only concrete `IBudgetProfiler`, so this corrects the memory
budget, the debug-view memory readout (`DebugViewProfilingSystem`), and the
analytics metrics (`PerformanceAnalyticsSystem`) in one place. In release
builds the counter reports 0, so player behavior is unchanged; the subtraction
only takes effect in the Editor and development builds — exactly where the
distortion occurs.

## How to verify

1. Enter play mode in the Editor and open the debug panel's memory widget.
2. Note the used-memory value and budget status.
3. Open the Profiler window and let it record for several minutes.
4. Expected: the reported used memory no longer creeps up with profiling time,
   and the budget status stays put. Before the fix it climbed steadily toward
   `WARNING`/`FULL`.

## Next steps

- [ ] Verify in the Editor per the steps above (with and without the Profiler
      window recording).
- [ ] Sanity-check a development build with the Profiler attached over
      autoconnect — "Profiler Used Memory" is non-zero there too and should be
      excluded the same way.
- [ ] Consider surfacing "Profiler Used Memory" as its own row in the memory
      debug widget so the exclusion is visible at a glance (optional,
      `DebugViewProfilingSystem.UpdateMemoryView`).
- [ ] Decide whether analytics should keep reporting the corrected values or
      also emit the raw counter for comparability with historical data
      (`PerformanceAnalyticsSystem` — `total_used_memory`,
      `system_used_memory`). Release builds are unaffected either way.
- [ ] Open PR against `dev` following `.github/PULL_REQUEST_TEMPLATE.md`;
      include the verification steps above as QA steps.
- [ ] Delete this file before merging.
