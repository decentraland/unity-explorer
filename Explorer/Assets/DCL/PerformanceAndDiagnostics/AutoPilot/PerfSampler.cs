using Cysharp.Threading.Tasks;
using DCL.Profiling;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace DCL.PerformanceAndDiagnostics.AutoPilot
{
    /// <summary>
    ///     Per-frame CPU/GPU time sampler that mirrors the original AutoPilot CSV +
    ///     summary writer, but is reusable from non-AutoPilot driving code (e.g.
    ///     AltTester-driven scenarios). The sampler is a plain static class so that
    ///     it can be configured once from the plugin system and then driven by
    ///     multiple, sequential fixtures within a single long-lived Player session.
    /// </summary>
    public static class PerfSampler
    {
        private static IProfiler profiler;
        private static StreamWriter csv;
        private static string currentCsvPath;
        private static string currentSummaryPath;
        private static bool sampling;
        private static int sampleLoopToken;

        /// <summary>
        ///     Wires the profiler used to read per-frame CPU and GPU times.
        ///     Must be called exactly once during plugin bootstrap, BEFORE any
        ///     call to <see cref="Begin"/>.
        /// </summary>
        public static void Configure(IProfiler profiler)
        {
            PerfSampler.profiler = profiler;
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
            if (profiler == null)
                throw new InvalidOperationException(
                    $"{nameof(PerfSampler)}.{nameof(Configure)} must be called before {nameof(Begin)}.");

            if (sampling)
            {
                Debug.LogWarning(
                    $"{nameof(PerfSampler)}.{nameof(Begin)} called while a sampling session is already active. Ignoring.");
                return;
            }

            if (string.IsNullOrEmpty(csvPath))
                throw new ArgumentException("CSV path is required", nameof(csvPath));

            csv = new StreamWriter(csvPath, false, new UTF8Encoding(false));
            csv.NewLine = "\r\n"; // https://www.rfc-editor.org/rfc/rfc4180
            csv.WriteLine("\"Frame\",\"CPU Time\",\"GPU Time\"");

            currentCsvPath = csvPath;
            currentSummaryPath = summaryPath;
            sampling = true;

            int token = ++sampleLoopToken;
            SampleLoopAsync(token).Forget();
        }

        /// <summary>
        ///     Closes the current sampling session: stops the sample loop, flushes
        ///     and closes the CSV, then (if a summary path was given) writes the
        ///     8-line summary file. No-op if there is no open session.
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

            if (!string.IsNullOrEmpty(currentSummaryPath))
                WriteSummary(currentCsvPath, currentSummaryPath);

            currentCsvPath = null;
            currentSummaryPath = null;
        }

        private static async UniTaskVoid SampleLoopAsync(int token)
        {
            while (sampling && token == sampleLoopToken)
            {
                WriteSample();
                await UniTask.Yield();
            }
        }

        private static void WriteSample()
        {
            if (csv == null)
                return;

            csv.WriteLine(string.Format(
                CultureInfo.InvariantCulture, "{0},{1},{2}",
                Time.frameCount,
                profiler.LastFrameTimeValueNs * 0.000001f,
                profiler.LastGpuFrameTimeValueNs * 0.000001f));
        }

        private static void WriteSummary(string csvFile, string summaryFile)
        {
            var cpuTimes = new List<float>();
            var gpuTimes = new List<float>();

            using (var reader = new StreamReader(csvFile))
            {
                reader.ReadLine(); // Discard the header line

                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();
                    if (line == null) break;
                    string[] columns = line.Split(',');
                    cpuTimes.Add(float.Parse(columns[1], CultureInfo.InvariantCulture));
                    gpuTimes.Add(float.Parse(columns[2], CultureInfo.InvariantCulture));
                }
            }

            using (var summary = new StreamWriter(summaryFile))
            {
                summary.Write("CPU average: ");
                summary.WriteLine(cpuTimes.Average().ToString(CultureInfo.InvariantCulture));
                summary.Write("CPU 1% worst: ");
                summary.WriteLine(PercentWorst(cpuTimes, 0.01f).ToString(CultureInfo.InvariantCulture));
                summary.Write("CPU 0.1% worst: ");
                summary.WriteLine(PercentWorst(cpuTimes, 0.001f).ToString(CultureInfo.InvariantCulture));
                summary.Write("CPU worst: ");
                summary.WriteLine(cpuTimes.Max().ToString(CultureInfo.InvariantCulture));
                summary.Write("GPU average: ");
                summary.WriteLine(gpuTimes.Average().ToString(CultureInfo.InvariantCulture));
                summary.Write("GPU 1% worst: ");
                summary.WriteLine(PercentWorst(gpuTimes, 0.01f).ToString(CultureInfo.InvariantCulture));
                summary.Write("GPU 0.1% worst: ");
                summary.WriteLine(PercentWorst(gpuTimes, 0.001f).ToString(CultureInfo.InvariantCulture));
                summary.Write("GPU worst: ");
                summary.WriteLine(gpuTimes.Max().ToString(CultureInfo.InvariantCulture));
            }
        }

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
