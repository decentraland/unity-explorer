using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.HealthChecks;
using System;
using System.Diagnostics;
using System.Threading;
using Utility.Types;

namespace DCL.UserInAppInitializationFlow.StartupOperations
{
    public class EnsureLivekitConnectionStartupOperation
    {
        private readonly IHealthCheck healthCheck;
        private const int TIMEOUT_IN_SECONDS = 20;

        public EnsureLivekitConnectionStartupOperation(IHealthCheck healthCheck)
        {
            this.healthCheck = healthCheck;
        }

        public async UniTask<EnumResult<TaskError>> LaunchLivekitConnection(CancellationToken ct)
        {
            try
            {
                var result = await healthCheck.IsRemoteAvailableAsync(ct).Timeout(TimeSpan.FromSeconds(TIMEOUT_IN_SECONDS));
                return result.AsEnumResult(TaskError.MessageError);
            }
            catch (TimeoutException)
            {
                ReportHub.Log(ReportCategory.LIVEKIT, $"Livekit handshake timed out");
                return EnumResult<TaskError>.ErrorResult(TaskError.Timeout,"Multiplayer services are offline. Try again later");
            }
        }

    }
}
