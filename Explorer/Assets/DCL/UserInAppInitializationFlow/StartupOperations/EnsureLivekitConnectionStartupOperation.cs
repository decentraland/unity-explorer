using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.Multiplayer.HealthChecks;
using System.Threading;
using Utility.Types;

namespace DCL.UserInAppInitializationFlow.StartupOperations
{
    public class EnsureLivekitConnectionStartupOperation : IStartupOperation
    {
        private readonly IHealthCheck healthCheck;

        public EnsureLivekitConnectionStartupOperation(IHealthCheck healthCheck)
        {
            this.healthCheck = healthCheck;
        }

        public async UniTask<Result> ExecuteAsync(AsyncLoadProcessReport report, CancellationToken ct)
        {
            var result = await healthCheck.IsRemoteAvailableAsync(ct);

            return result.success
                ? Result.SuccessResult()
                : Result.ErrorResult(result.errorMessage!);
        }
    }
}
