using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.HealthChecks;
using DCL.Utility.Types;
using System;
using System.Threading;

namespace DCL.UserInAppInitializationFlow
{
    public class EnsureLivekitConnectionStartupOperation
    {
        private readonly IHealthCheck healthCheck;
        private readonly IRoomHub roomHub;
        private static readonly TimeSpan LIVEKIT_TIMEOUT = TimeSpan.FromSeconds(30);

        public EnsureLivekitConnectionStartupOperation(IHealthCheck healthCheck, IRoomHub roomHub)
        {
            this.healthCheck = healthCheck;
            this.roomHub = roomHub;
        }

        public async UniTask<EnumResult<TaskError>> LaunchLivekitConnectionAsync(CancellationToken ct)
        {
            try
            {
                var result = await healthCheck.IsRemoteAvailableAsync(ct).Timeout(LIVEKIT_TIMEOUT);
                return result.AsEnumResult(TaskError.MessageError);
            }
            catch (TimeoutException)
            {
                ReportHub.Log(ReportCategory.LIVEKIT, $"Livekit handshake timed out");

                try
                {
                    // Fixes: https://github.com/decentraland/unity-explorer/issues/5346
                    // We need to manually stop in case of timeout. Livekit keeps trying to connect (thus cancellation tokens are not supported)
                    // and will skip the process the next time this task is executed
                    await roomHub.StopAsync().Timeout(LIVEKIT_TIMEOUT);
                }
                catch (Exception e) { ReportHub.LogException(e, ReportCategory.LIVEKIT); }

                return EnumResult<TaskError>.ErrorResult(TaskError.Timeout, "Multiplayer services are offline. Try again later");
            }
        }
    }
}
