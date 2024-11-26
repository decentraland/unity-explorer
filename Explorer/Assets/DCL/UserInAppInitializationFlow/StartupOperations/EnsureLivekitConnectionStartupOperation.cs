using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.Multiplayer.HealthChecks;
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

        public async UniTask<Result> ExecuteAsync(AsyncLoadProcessReport report, CancellationToken ct)
        {
            float finalizationProgress = loadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.LiveKitConnectionEnsuring);
            Result result = await healthCheck.IsRemoteAvailableAsync(ct);

            if (result.Success)
                report.SetProgress(finalizationProgress);

            return result;
        }
    }
}
