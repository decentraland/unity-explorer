using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.Multiplayer.HealthChecks;
using System.Threading;
using Utility.Types;

namespace DCL.UserInAppInitializationFlow.StartupOperations
{
    public class EnsureLivekitConnectionStartupOperation : IStartupOperation
    {
        private readonly RealFlowLoadingStatus loadingStatus;
        private readonly IHealthCheck healthCheck;

        public EnsureLivekitConnectionStartupOperation(RealFlowLoadingStatus loadingStatus, IHealthCheck healthCheck)
        {
            this.loadingStatus = loadingStatus;
            this.healthCheck = healthCheck;
        }

        public async UniTask<Result> ExecuteAsync(AsyncLoadProcessReport report, CancellationToken ct)
        {
            (bool success, string? errorMessage) result = await healthCheck.IsRemoteAvailableAsync(ct);

            if (result.success)
                report.SetProgress(loadingStatus.SetStage(RealFlowLoadingStatus.Stage.LiveKitConnectionEnsured));

            return result.success
                ? Result.SuccessResult()
                : Result.ErrorResult(result.errorMessage!);
        }
    }
}
