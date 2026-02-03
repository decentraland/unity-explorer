using Cysharp.Threading.Tasks;
using DCL.Profiling;
using DCL.RealmNavigation;
using Global.AppArgs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Profiler = UnityEngine.Profiling.Profiler;

namespace DCL.PerformanceAndDiagnostics.AutoPilot
{
    public sealed class AutoPilot
    {
        private readonly IAppArgs appArgs;
        private readonly ILoadingStatus loadingStatus;
        private readonly IProfiler profiler;
        private StreamWriter csv;

        public AutoPilot(IAppArgs appArgs, ILoadingStatus loadingStatus, IProfiler profiler)
        {
            this.appArgs = appArgs;
            this.loadingStatus = loadingStatus;
            this.profiler = profiler;
        }

        public async UniTask RunAsync()
        {
            StreamWriter csv = null;
            var exitCode = 0;

            try
            {
                if (appArgs.TryGetValue(AppArgsFlags.AUTOPILOT_CSV,
                        out string csvFile))
                {
                    if (csvFile == null)
                        throw new Exception($"{nameof(csvFile)} is null");

                    csv = new StreamWriter(csvFile, false, new UTF8Encoding(false));
                    csv.NewLine = "\r\n"; // https://www.rfc-editor.org/rfc/rfc4180
                    await csv.WriteLineAsync("\"Frame\",\"CPU Time\",\"GPU Time\"");
                }

                while (loadingStatus.CurrentStage.Value != LoadingStatus.LoadingStage.Completed)
                    await UniTask.Yield();

                if (appArgs.TryGetValue(AppArgsFlags.AUTOPILOT_RAW,
                        out string logFile))
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Profiler.logFile = logFile;
                    Profiler.enableBinaryLog = true;
                    Profiler.enabled = true;
#else
                    DCL.Diagnostics.ReportHub.LogWarning(
                        DCL.Diagnostics.ReportCategory.ALWAYS,
                        $"You set the --{AppArgsFlags.PROFILER_LOG_FILE} argument, but the profiler is only available in development builds.");
#endif
                }

                await StandAtSpawnAsync();

                if (csv != null &&
                    appArgs.TryGetValue(AppArgsFlags.AUTOPILOT_SUMMARY,
                        out string summaryFile))
                {
                    await csv.DisposeAsync();
                    csv = null;
                    await WriteSummary(csvFile, summaryFile);
                }
            }
            catch (Exception ex)
            {
                if (csv != null)
                    await csv.WriteLineAsync(
                        $"\"Error: {ex.Message.Replace("\"", "\"\"")}\"");

                exitCode = ex.HResult;
                throw;
            }
            finally
            {
                if (csv != null)
                    await csv.DisposeAsync();

                Application.Quit(exitCode);
            }
        }

        /// <summary>
        /// The minimal performance test: stand at spawn for 1000 frames.
        /// </summary>
        private async UniTask StandAtSpawnAsync(StreamWriter csv)
        {
            for (var i = 0; i < 1000; i++)
            {
                await WriteSampleAsync();
                await UniTask.Yield();
            }
        }

        private Task WriteSampleAsync() =>
            csv.WriteLineAsync(
                $"{Time.frameCount},{profiler.LastFrameTimeValueNs},{profiler.LastGpuFrameTimeValueNs}");

        private static async UniTask WriteSummary(string csvFile,
            string summaryFile)
        {
            var cpuTimes = new List<float>();
            var gpuTimes = new List<float>();

            using (var csv = new StreamReader(csvFile))
            {
                await csv.ReadLineAsync(); // Discard the header line

                while (!csv.EndOfStream)
                {
                    string line = await csv.ReadLineAsync();
                    string[] columns = line.Split(',');
                    cpuTimes.Add(float.Parse(columns[1]));
                    gpuTimes.Add(float.Parse(columns[2]));
                }
            }

            await using (var summary = new StreamWriter(summaryFile))
            {
                summary.WriteLine($"CPU average: {cpuTimes.Average()}");
                summary.WriteLine($"CPU 1% worst: {PercentWorst(cpuTimes, 0.01f)}");
                summary.WriteLine($"CPU 0.1% worst: {PercentWorst(cpuTimes, 0.001f)}");
                summary.WriteLine($"CPU worst: {cpuTimes.Max()}");
                summary.WriteLine($"GPU average: {gpuTimes.Average()}");
                summary.WriteLine($"GPU 1% worst: {PercentWorst(gpuTimes, 0.01f)}");
                summary.WriteLine($"GPU 0.1% worst: {PercentWorst(gpuTimes, 0.001f)}");
                summary.WriteLine($"GPU worst: {gpuTimes.Max()}");
            }
        }

        /// <remarks>
        /// As done by GamersNexus:
        /// https://www.youtube.com/watch?v=WcTxrzFqdyw#t=34m17s
        /// </remarks>
        private static float PercentWorst(List<float> times, float fraction) =>
            times.OrderByDescending(i => i)
               .Take((int)(times.Count * fraction))
               .Average();
    }
}
