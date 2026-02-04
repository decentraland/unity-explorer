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

                if (appArgs.TryGetValue(AppArgsFlags.AUTOPILOT_SUMMARY, out string summaryFile)
                    && csv == null)
                    throw new Exception(
                        $"--{AppArgsFlags.AUTOPILOT_SUMMARY} requires --{AppArgsFlags.AUTOPILOT_CSV}");

                while (loadingStatus.CurrentStage.Value != LoadingStatus.LoadingStage.Completed)
                    await UniTask.Yield();

                await StandAtSpawnAsync();

                if (summaryFile != null)
                {
                    await csv.DisposeAsync();
                    csv = null;
                    await WriteSummaryAsync(csvFile, summaryFile);
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
        /// The minimal performance test: stand at spawn for one minute.
        /// </summary>
        private async UniTask StandAtSpawnAsync()
        {
            float startTime = Time.realtimeSinceStartup;

            while (Time.realtimeSinceStartup - startTime < 60f)
            {
                await WriteSampleAsync();
                await UniTask.Yield();
            }
        }

        private Task WriteSampleAsync() =>
            csv != null
                ? csv.WriteLineAsync(
                    $"{Time.frameCount},{profiler.LastFrameTimeValueNs * 0.000001f},{profiler.LastGpuFrameTimeValueNs * 0.000001f}")
                : Task.CompletedTask;

        private static async UniTask WriteSummaryAsync(string csvFile,
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
                await summary.WriteLineAsync($"CPU average: {cpuTimes.Average()}");
                await summary.WriteLineAsync($"CPU 1% worst: {PercentWorst(cpuTimes, 0.01f)}");
                await summary.WriteLineAsync($"CPU 0.1% worst: {PercentWorst(cpuTimes, 0.001f)}");
                await summary.WriteLineAsync($"CPU worst: {cpuTimes.Max()}");
                await summary.WriteLineAsync($"GPU average: {gpuTimes.Average()}");
                await summary.WriteLineAsync($"GPU 1% worst: {PercentWorst(gpuTimes, 0.01f)}");
                await summary.WriteLineAsync($"GPU 0.1% worst: {PercentWorst(gpuTimes, 0.001f)}");
                await summary.WriteLineAsync($"GPU worst: {gpuTimes.Max()}");
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
