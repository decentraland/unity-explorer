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
            loadingStatus.SetCurrentStage(LoadingStatus.CurrentStage.EnsuringLiveKitConnection);
            (bool success, string? errorMessage) result = await healthCheck.IsRemoteAvailableAsync(ct);

            if (result.success)
                report.SetProgress(loadingStatus.SetCompletedStage(LoadingStatus.CompletedStage.LiveKitConnectionEnsured));

            return result.success
                ? Result.SuccessResult()
                : Result.ErrorResult(result.errorMessage!);
        }
    }
}
