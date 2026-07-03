using Cysharp.Threading.Tasks;
using DCL.RealmNavigation;
using Global.AppArgs;
using System;
using UnityEngine;
using Profiler = UnityEngine.Profiling.Profiler;

namespace DCL.PerformanceAndDiagnostics.AutoPilot
{
    public sealed class AutoPilot
    {
        private readonly IAppArgs appArgs;
        private readonly ILoadingStatus loadingStatus;

        public AutoPilot(IAppArgs appArgs, ILoadingStatus loadingStatus)
        {
            this.appArgs = appArgs;
            this.loadingStatus = loadingStatus;
        }

        public async UniTask RunAsync()
        {
            var exitCode = 0;
            var sessionStarted = false;

            try
            {
                if (!appArgs.TryGetValue(AppArgsFlags.AUTOPILOT_CSV, out string csvFile))
                    return;

                if (csvFile == null)
                    throw new Exception($"{nameof(csvFile)} is null");

                appArgs.TryGetValue(AppArgsFlags.AUTOPILOT_SUMMARY, out string summaryFile);

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

                PerfSampler.Begin(csvFile, summaryFile);
                sessionStarted = true;
                await StandAtSpawnAsync();
                PerfSampler.End();
                sessionStarted = false;
            }
            catch (Exception ex)
            {
                exitCode = ex.HResult;
                throw;
            }
            finally
            {
                if (sessionStarted)
                    PerfSampler.End();

                Application.Quit(exitCode);
            }
        }

        /// <summary>
        /// The minimal performance test: stand at spawn for one minute.
        /// </summary>
        private static async UniTask StandAtSpawnAsync()
        {
            float startTime = UnityEngine.Time.realtimeSinceStartup;

            while (UnityEngine.Time.realtimeSinceStartup - startTime < 90f)
                await UniTask.Yield();
        }
    }
}
