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
                if (!appArgs.TryGetValue(AppArgsFlags.AUTOPILOT, out string outPath)
                    || outPath == null)
                    throw new Exception("Did not specify automated test output path");

                csv = new StreamWriter(outPath, false, new UTF8Encoding(false));
                csv.NewLine = "\r\n"; // https://www.rfc-editor.org/rfc/rfc4180
                await csv.WriteLineAsync("\"Frame\",\"CPU Time\",\"GPU Time\"");

                while (loadingStatus.CurrentStage.Value != LoadingStatus.LoadingStage.Completed)
                    await UniTask.Yield();

                await StandAtSpawnTest(csv);
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
        private async UniTask StandAtSpawnTest(StreamWriter csv)
        {
            for (var i = 0; i < 1000; i++)
            {
                await csv.WriteLineAsync(
                    $"{Time.frameCount},{profiler.LastFrameTimeValueNs},{profiler.LastGpuFrameTimeValueNs}");

                await UniTask.Yield();
            }
        }
    }
}
