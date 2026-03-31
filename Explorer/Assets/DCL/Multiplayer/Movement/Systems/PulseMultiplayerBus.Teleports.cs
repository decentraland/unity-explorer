using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Movement;
using Decentraland.Pulse;
using System.Threading;

namespace DCL.Multiplayer.Connections.Pulse
{
    public partial class PulseMultiplayerBus
    {
        private async UniTask SubscribeToTeleportsAsync(CancellationToken ct)
        {
            await foreach (TeleportPerformed teleport in pulseService.SubscribeAsync<TeleportPerformed>(
                               ServerMessage.MessageOneofCase.Teleported, ct))
            {
                if (isDisposed)
                {
                    ReportHub.LogError(ReportCategory.MULTIPLAYER, "Receiving teleport while disposed");
                    break;
                }

                if (!peerIdCache.TryGetWallet(teleport.SubjectId, out string wallet))
                {
                    ReportHub.LogError(ReportCategory.MULTIPLAYER, $"Receiving teleport from unknown peer: {teleport.SubjectId}");
                    continue;
                }

                NetworkMovementMessage movementMessage = ToNetworkMovementMessage(teleport.State, teleport.ServerTick, isInstant: true);
                TryUpdateLastMovementAndCompleteResync(teleport.SubjectId, teleport.Sequence, movementMessage);
                Inbox(movementMessage, wallet);
            }
        }
    }
}
