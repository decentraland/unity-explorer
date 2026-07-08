using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Movement;
using System.Threading;

namespace DCL.RealmNavigation.TeleportOperations
{
    // Previous-realm peers would otherwise keep streaming over Pulse during loading and briefly materialize as avatars once it completes.
    public class BlockPulseIngressTeleportOperation : TeleportOperationBase
    {
        private readonly IPulseIngressBlocker pulseMultiplayerBus;

        public BlockPulseIngressTeleportOperation(IPulseIngressBlocker pulseMultiplayerBus)
        {
            this.pulseMultiplayerBus = pulseMultiplayerBus;
        }

        protected override UniTask InternalExecuteAsync(TeleportParams teleportParams, CancellationToken ct)
        {
            pulseMultiplayerBus.BlockIngressUntilTeleportBroadcast();
            return UniTask.CompletedTask;
        }
    }
}
