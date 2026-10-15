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
        private readonly PulseRealm pulseRealm;
        private readonly IRealmData realmData;

        public ConnectPulseTeleportOperation(IPulseMultiplayerService pulseMultiplayerService, PulseActivation pulseActivation, PulseRealm pulseRealm, IRealmData realmData)
        {
            this.pulseMultiplayerService = pulseMultiplayerService;
            this.pulseActivation = pulseActivation;
            this.pulseRealm = pulseRealm;
            this.realmData = realmData;
        }

        protected override async UniTask InternalExecuteAsync(TeleportParams teleportParams, CancellationToken ct)
        {
            if (!pulseActivation.IsActive || realmData.IsCommsOffline())
                return;

            // The realm ships in the handshake's initial state, and an empty one violates the server contract
            await pulseRealm.EnsureResolvedAsync(ct);

            if (string.IsNullOrEmpty(pulseRealm.Value))
            {
                ReportHub.LogWarning(ReportCategory.MULTIPLAYER, "Pulse realm could not be resolved; Pulse is not connected for this realm.");
                return;
            }

            ConnectAsync().Forget();
        }

        private async UniTaskVoid ConnectAsync()
        {
            try
            {
                ReportHub.Log(ReportCategory.MULTIPLAYER, $"Connecting Pulse for realm {realmData.RealmName}");

                if (!await pulseMultiplayerService.ConnectAsync(CancellationToken.None))
                    ReportHub.LogWarning(ReportCategory.MULTIPLAYER, $"Pulse connection failed terminally for realm {realmData.RealmName}");
            }
            catch (OperationCanceledException) { }
            catch (Exception exception) { ReportHub.LogException(exception, ReportCategory.MULTIPLAYER); }
        }
    }
}
