using Cysharp.Threading.Tasks;
using DCL.Profiling;
using DCL.RealmNavigation;
using Global.AppArgs;
using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace DCL.PerformanceAndDiagnostics.AutoPilot
{
    public sealed class AutoPilot
    {
        private readonly IAppArgs appArgs;
        private readonly ILoadingStatus loadingStatus;
        private readonly IProfiler profiler;

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

                if (appArgs.TryGetValue(AppArgsFlags.PROFILER_LOG_FILE,
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

                await StandAtSpawnTest();
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
        private async UniTask StandAtSpawnTest()
        {
            for (var i = 0; i < 1000; i++)
            {
                await WriteSample();
                await UniTask.Yield();
            }
        }

        private Task WriteSample() =>
            csv.WriteLineAsync(
                $"{Time.frameCount},{profiler.LastFrameTimeValueNs},{profiler.LastGpuFrameTimeValueNs}");
    }
}
