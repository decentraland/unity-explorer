using Cysharp.Threading.Tasks;
using DCL.Profiling;
using DCL.RealmNavigation;
using Global.AppArgs;
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
            await using var writer = new StreamWriter("autopilot.csv", false,
                new UTF8Encoding(false));

            writer.NewLine = "\r\n"; // https://www.rfc-editor.org/rfc/rfc4180
            await writer.WriteLineAsync("\"Frame\",\"CPU Time\",\"GPU Time\"");

            while (loadingStatus.CurrentStage.Value != LoadingStatus.LoadingStage.Completed)
                await UniTask.Yield();

            // The minimal performance test: stand at spawn for 1000 frames.
            for (var i = 0; i < 1000; i++)
            {
                await writer.WriteLineAsync(
                    $"{Time.frameCount},{profiler.LastFrameTimeValueNs},{profiler.LastGpuFrameTimeValueNs}");

                await UniTask.Yield();
            }

            Application.Quit(0);
        }
    }
}
