using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.HealthChecks;
using System;
using System.Threading;
using UnityEngine;
using Utility.Types;

namespace DCL.UserInAppInitializationFlow.StartupOperations
{
    public class EnsureLivekitConnectionStartupOperation
    {
        private readonly IHealthCheck healthCheck;
        private static readonly TimeSpan LIVEKIT_TIMEOUT = TimeSpan.FromSeconds(30);

        public EnsureLivekitConnectionStartupOperation(IHealthCheck healthCheck)
        {
            this.healthCheck = healthCheck;
        }

        public async UniTask<EnumResult<TaskError>> LaunchLivekitConnectionAsync(CancellationToken ct)
        {
            try
            {
                Debug.Log("JUANI LIVEKIT START");
                var result = await healthCheck.IsRemoteAvailableAsync(ct).Timeout(LIVEKIT_TIMEOUT);
                Debug.Log("JUANI LIVEKIT END");
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
