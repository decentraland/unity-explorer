using Cysharp.Threading.Tasks;
using DCL.Multiplayer.HealthChecks;
using DCL.RealmNavigation;
using System;
using System.Diagnostics;
using System.Threading;
using Utility.Types;

namespace DCL.UserInAppInitializationFlow.StartupOperations
{
    public class EnsureLivekitConnectionStartupOperation : IStartupOperation
    {
        private readonly ILoadingStatus loadingStatus;
        private readonly IHealthCheck healthCheck;

        public EnsureLivekitConnectionStartupOperation(ILoadingStatus loadingStatus, IHealthCheck healthCheck)
        {
            this.loadingStatus = loadingStatus;
            this.healthCheck = healthCheck;
        }

        public async UniTask<EnumResult<TaskError>> ExecuteAsync(IStartupOperation.Params report, CancellationToken ct)
        {
            float finalizationProgress = loadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.LiveKitConnectionEnsuring);
            DoConnection(ct).Forget();
            return EnumResult<TaskError>.SuccessResult();
            //if (result.Success)
            //    report.Report.SetProgress(finalizationProgress);

            //return result.AsEnumResult(TaskError.MessageError);
        }

        public async UniTask DoConnection(CancellationToken ct)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            UnityEngine.Debug.Log("JUANI LIVEKIT STARTED CONNECTING");
            await healthCheck.IsRemoteAvailableAsync(ct).Timeout(TimeSpan.FromSeconds(10));
            stopwatch.Stop();
            UnityEngine.Debug.Log($"JUANI LIVEKIT CONNECTED {stopwatch.ElapsedMilliseconds} ms");
        }
    }
}
