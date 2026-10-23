using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.Pulse;
using ECS;
using System;
using System.Threading;

namespace DCL.RealmNavigation.TeleportOperations
{
    /// <summary>
    ///     Starts the Pulse connection for the current realm without awaiting it, so the loading screen is not
    ///     blocked by the handshake. No-op when Pulse is not the active transport or the realm's comms adapter
    ///     is offline. The connection's lifetime is owned by the service session, not by this operation.
    /// </summary>
    public class ConnectPulseTeleportOperation : TeleportOperationBase
    {
        private readonly IPulseMultiplayerService pulseMultiplayerService;
        private readonly PulseActivation pulseActivation;
        private readonly IRealmData realmData;

        public ConnectPulseTeleportOperation(IPulseMultiplayerService pulseMultiplayerService, PulseActivation pulseActivation, IRealmData realmData)
        {
            this.pulseMultiplayerService = pulseMultiplayerService;
            this.pulseActivation = pulseActivation;
            this.realmData = realmData;
        }

        protected override UniTask InternalExecuteAsync(TeleportParams teleportParams, CancellationToken ct)
        {
            if (pulseActivation.IsActive && !realmData.IsCommsOffline())
                ConnectAsync().Forget();

            return UniTask.CompletedTask;
        }

        private async UniTaskVoid ConnectAsync()
        {
            try
            {
                ReportHub.Log(ReportCategory.MULTIPLAYER, $"Connecting Pulse for realm {realmData.RealmName}");

                // With unbounded retries a failure is terminal (e.g. the handshake is rejected): same fallback as at start-up.
                if (!await pulseMultiplayerService.ConnectAsync(CancellationToken.None))
                {
                    pulseActivation.Deactivate();
                    ReportHub.LogWarning(ReportCategory.MULTIPLAYER, "Pulse connection failed terminally; falling back to LiveKit-only.");
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception exception) { ReportHub.LogException(exception, ReportCategory.MULTIPLAYER); }
        }
    }
}
