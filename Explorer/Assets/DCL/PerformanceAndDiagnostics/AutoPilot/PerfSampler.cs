using Cysharp.Threading.Tasks;
using DCL.Profiling;
using ECS.SceneLifeCycle;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace DCL.PerformanceAndDiagnostics.AutoPilot
{
    /// <summary>
    ///     Per-frame CPU/GPU time, memory and loaded scene count sampler that mirrors
    ///     the original AutoPilot CSV + summary writer, but is reusable from
    ///     non-AutoPilot driving code (e.g. AltTester-driven scenarios). The sampler is
    ///     a plain static class so that it can be configured once from the plugin
    ///     system and then driven by multiple, sequential fixtures within a single
    ///     long-lived Player session.
    /// </summary>
    public static class PerfSampler
    {
        private const float BYTES_IN_MB = 1024f * 1024f;

        private static IProfiler? profiler;
        private static IScenesCache? scenesCache;
        private static StreamWriter? csv;
        private static string? currentCsvPath;
        private static string? currentSummaryPath;
        private static bool sampling;
        private static int sampleLoopToken;
        private static float startupToBeginSeconds;

        /// <summary>
        ///     Wires the profiler used to read per-frame CPU, GPU and memory values and
        ///     the scenes cache used to read the loaded scene count.
        ///     Must be called exactly once during plugin bootstrap, BEFORE any
        ///     call to <see cref="Begin"/>.
        /// </summary>
        public static void Configure(IProfiler profiler, IScenesCache scenesCache)
        {
            PerfSampler.profiler = profiler;
            PerfSampler.scenesCache = scenesCache;
        }

        /// <summary>
        ///     Opens a new sampling session: writes the CSV header, remembers the
        ///     summary path, and kicks off the per-frame sampling loop. If a session
        ///     is already open this is logged and the call becomes a no-op (the
        ///     existing session keeps running). Throws if <see cref="Configure"/>
        ///     was never called.
        /// </summary>
        public static void Begin(string csvPath, string summaryPath)
        {
            if (profiler == null || scenesCache == null)
                throw new InvalidOperationException(
                    $"{nameof(PerfSampler)}.{nameof(Configure)} must be called before {nameof(Begin)}.");

            if (sampling)
            {
                Diagnostics.ReportHub.LogWarning(
                    Diagnostics.ReportCategory.ALWAYS,
                    $"{nameof(PerfSampler)}.{nameof(Begin)} called while a sampling session is already active. Ignoring.");
                return;
            }

            if (string.IsNullOrEmpty(csvPath))
                throw new ArgumentException("CSV path is required", nameof(csvPath));

            csv = new StreamWriter(csvPath, false, new UTF8Encoding(false));
            csv.NewLine = "\r\n"; // https://www.rfc-editor.org/rfc/rfc4180
            csv.WriteLine("\"Frame\",\"CPU Time\",\"GPU Time\",\"System Used MB\",\"Total Used MB\",\"Loaded Scenes\"");

            currentCsvPath = csvPath;
            currentSummaryPath = summaryPath;
            startupToBeginSeconds = UnityEngine.Time.realtimeSinceStartup;
            sampling = true;

            int token = ++sampleLoopToken;
            SampleLoopAsync(token).Forget();
        }

        /// <summary>
        ///     Closes the current sampling session: stops the sample loop, flushes
        ///     and closes the CSV, then (if a summary path was given) writes the
        ///     summary file. No-op if there is no open session.
        /// </summary>
        public static void End()
        {
            if (!sampling)
                return;

            sampling = false;

            try
            {
                csv?.Dispose();
            }
            finally
            {
                csv = null;
            }

            string? csvPath = currentCsvPath;
            string? summaryPath = currentSummaryPath;

            if (!string.IsNullOrEmpty(csvPath) && !string.IsNullOrEmpty(summaryPath))
                WriteSummary(csvPath, summaryPath, startupToBeginSeconds);

            currentCsvPath = null;
            currentSummaryPath = null;
            startupToBeginSeconds = 0f;
        }

        private static async UniTaskVoid SampleLoopAsync(int token)
        {
            try
            {
                while (sampling && token == sampleLoopToken)
                {
                    WriteSample();
                    await UniTask.Yield();
                }
            }
            catch (Exception e)
            {
                Diagnostics.ReportHub.LogException(e, Diagnostics.ReportCategory.ALWAYS);
                sampling = false;
            }
        }

        private static void WriteSample()
        {
            if (csv == null || profiler == null || scenesCache == null)
                return;

            csv.WriteLine(string.Format(
                CultureInfo.InvariantCulture, "{0},{1},{2},{3},{4},{5}",
                UnityEngine.Time.frameCount,
                profiler.LastFrameTimeValueNs * 0.000001f,
                profiler.LastGpuFrameTimeValueNs * 0.000001f,
                profiler.SystemUsedMemoryInBytes / BYTES_IN_MB,
                profiler.TotalUsedMemoryInBytes / BYTES_IN_MB,
                scenesCache.Scenes.Count));
        }

        private static void WriteSummary(string csvFile, string summaryFile, float secondsToSamplingStart)
        {
            var cpuTimes = new List<float>();
            var gpuTimes = new List<float>();
            var systemUsedMb = new List<float>();
            var totalUsedMb = new List<float>();
            var loadedScenes = new List<float>();

            using (var reader = new StreamReader(csvFile))
            {
                reader.ReadLine(); // Discard the header line

                while (!reader.EndOfStream)
                {
                    string? line = reader.ReadLine();
                    if (line == null) break;
                    string[] columns = line.Split(',');

                    // A session killed mid-write can leave a truncated final row.
                    if (columns.Length < 6) continue;
                    if (!float.TryParse(columns[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float cpu)) continue;
                    if (!float.TryParse(columns[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float gpu)) continue;
                    if (!float.TryParse(columns[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float systemMb)) continue;
                    if (!float.TryParse(columns[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float totalMb)) continue;
                    if (!float.TryParse(columns[5], NumberStyles.Float, CultureInfo.InvariantCulture, out float scenes)) continue;

                    cpuTimes.Add(cpu);
                    gpuTimes.Add(gpu);
                    systemUsedMb.Add(systemMb);
                    totalUsedMb.Add(totalMb);
                    loadedScenes.Add(scenes);
                }
            }

            using (var summary = new StreamWriter(summaryFile))
            {
                summary.Write("CPU average: ");
                summary.WriteLine(Average(cpuTimes).ToString(CultureInfo.InvariantCulture));
                summary.Write("CPU 1% worst: ");
                summary.WriteLine(PercentWorst(cpuTimes, 0.01f).ToString(CultureInfo.InvariantCulture));
                summary.Write("CPU 0.1% worst: ");
                summary.WriteLine(PercentWorst(cpuTimes, 0.001f).ToString(CultureInfo.InvariantCulture));
                summary.Write("CPU worst: ");
                summary.WriteLine(Peak(cpuTimes).ToString(CultureInfo.InvariantCulture));
                summary.Write("GPU average: ");
                summary.WriteLine(Average(gpuTimes).ToString(CultureInfo.InvariantCulture));
                summary.Write("GPU 1% worst: ");
                summary.WriteLine(PercentWorst(gpuTimes, 0.01f).ToString(CultureInfo.InvariantCulture));
                summary.Write("GPU 0.1% worst: ");
                summary.WriteLine(PercentWorst(gpuTimes, 0.001f).ToString(CultureInfo.InvariantCulture));
                summary.Write("GPU worst: ");
                summary.WriteLine(Peak(gpuTimes).ToString(CultureInfo.InvariantCulture));
                summary.Write("System used MB average: ");
                summary.WriteLine(Average(systemUsedMb).ToString(CultureInfo.InvariantCulture));
                summary.Write("System used MB peak: ");
                summary.WriteLine(Peak(systemUsedMb).ToString(CultureInfo.InvariantCulture));
                summary.Write("Total used MB average: ");
                summary.WriteLine(Average(totalUsedMb).ToString(CultureInfo.InvariantCulture));
                summary.Write("Total used MB peak: ");
                summary.WriteLine(Peak(totalUsedMb).ToString(CultureInfo.InvariantCulture));
                summary.Write("Loaded scenes average: ");
                summary.WriteLine(Average(loadedScenes).ToString(CultureInfo.InvariantCulture));
                summary.Write("Loaded scenes peak: ");
                summary.WriteLine(Peak(loadedScenes).ToString(CultureInfo.InvariantCulture));
                summary.Write("Seconds to world loaded: ");
                summary.WriteLine(secondsToSamplingStart.ToString(CultureInfo.InvariantCulture));
            }
        }

        private static float Average(List<float> values) =>
            values.Count == 0 ? 0f : values.Average();

        private static float Peak(List<float> values) =>
            values.Count == 0 ? 0f : values.Max();

        /// <remarks>
        ///     As done by GamersNexus:
        ///     https://www.youtube.com/watch?v=WcTxrzFqdyw#t=34m17s
        /// </remarks>
        private static float PercentWorst(List<float> times, float fraction)
        {
            // Short sampling windows can yield < 1/fraction samples (e.g. an
            // InWorld fixture that runs ~15s at ~30 FPS produces ~450 samples,
            // so (int)(450 * 0.001f) is 0 and Take(0).Average() throws). Floor
            // the count to 1 so 0.1% worst on small windows just reports the
            // single worst frame instead of crashing summary generation.
            if (times.Count == 0) return 0f;
            var k = Math.Max(1, (int)(times.Count * fraction));
            return times.OrderByDescending(i => i).Take(k).Average();
        }
    }
}
