using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.Pulse;
using ECS;
using System.Threading;

namespace DCL.RealmNavigation.TeleportOperations
{
    /// <summary>
    ///     Disconnects Pulse when the current realm is single-player (comms adapter "offline:offline").
    ///     No-op when Pulse is not the active transport or the realm has comms.
    /// </summary>
    public class DisconnectPulseInOfflineRealmTeleportOperation : TeleportOperationBase
    {
        private readonly IPulseMultiplayerService pulseMultiplayerService;
        private readonly PulseActivation pulseActivation;
        private readonly IRealmData realmData;

        public DisconnectPulseInOfflineRealmTeleportOperation(IPulseMultiplayerService pulseMultiplayerService, PulseActivation pulseActivation, IRealmData realmData)
        {
            this.pulseMultiplayerService = pulseMultiplayerService;
            this.pulseActivation = pulseActivation;
            this.realmData = realmData;
        }

        protected override async UniTask InternalExecuteAsync(TeleportParams teleportParams, CancellationToken ct)
        {
            if (!pulseActivation.IsActive || !realmData.IsCommsOffline())
                return;

            ReportHub.Log(ReportCategory.MULTIPLAYER, $"Realm {realmData.RealmName} has an offline comms adapter: disconnecting Pulse");
            await pulseMultiplayerService.DisconnectAsync();
        }
    }
}
