#nullable enable

using System.Diagnostics;
using DCL.Diagnostics;

namespace DCL.Utility
{
    /// <summary>
    ///     Measures per-step durations during shutdown and logs them via <see cref="ReportHub.LogProductionInfo" />.
    ///     Mutable struct: keep it as a local variable, do not copy it around.
    /// </summary>
    public struct ShutdownStopwatch
    {
        private readonly string prefix;
        private readonly Stopwatch stopwatch;
        private long stepStartedAtMs;

        public long ElapsedMilliseconds => stopwatch.ElapsedMilliseconds;

        private ShutdownStopwatch(string prefix)
        {
            this.prefix = prefix;
            stopwatch = Stopwatch.StartNew();
            stepStartedAtMs = 0;
        }

        public static ShutdownStopwatch StartNew(string prefix) =>
            new (prefix);

        public void LogStep(string step)
        {
            long totalMs = stopwatch.ElapsedMilliseconds;
            ReportHub.LogProductionInfo($"[{prefix}] '{step}' took {totalMs - stepStartedAtMs}ms (total {totalMs}ms)");
            stepStartedAtMs = totalMs;
        }
    }
}
