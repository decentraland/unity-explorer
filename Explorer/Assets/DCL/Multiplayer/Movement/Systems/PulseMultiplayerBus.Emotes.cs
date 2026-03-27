using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Emotes;
using DCL.Multiplayer.Movement;
using Decentraland.Pulse;
using System.Threading;

namespace DCL.Multiplayer.Connections.Pulse
{
    public partial class PulseMultiplayerBus
    {
        private async UniTask SubscribeToEmoteStartedAsync(CancellationToken ct)
        {
            await foreach (EmoteStarted emoteStarted in pulseService.SubscribeAsync<EmoteStarted>(
                               ServerMessage.MessageOneofCase.EmoteStarted, ct))
            {
                if (isDisposed)
                {
                    ReportHub.LogError(ReportCategory.MULTIPLAYER, "Receiving emote started while disposed");
                    break;
                }

                if (!peerIdCache.TryGetWallet(emoteStarted.SubjectId, out string walletId))
                {
                    ReportHub.LogWarning(ReportCategory.MULTIPLAYER, $"Received EmoteStarted from unknown peer {emoteStarted.SubjectId}");
                    continue;
                }

                if (emoteStarted.PlayerState != null)
                {
                    NetworkMovementMessage movementMessage = ToNetworkMovementMessage(emoteStarted.PlayerState, emoteStarted.ServerTick, isInstant: true, isEmoting: true);
                    Inbox(movementMessage, walletId);
                }

                double timestamp = emoteStarted.ServerTick * SERVER_TICKS_TO_MOVEMENT_TIMESTAMP;
                emotesMessageBus.Enqueue(new RemoteEmoteIntention(new URN(emoteStarted.EmoteId), walletId, timestamp));
            }
        }

        private async UniTask SubscribeToEmoteStoppedAsync(CancellationToken ct)
        {
            await foreach (EmoteStopped emoteStopped in pulseService.SubscribeAsync<EmoteStopped>(
                               ServerMessage.MessageOneofCase.EmoteStopped, ct))
            {
                if (isDisposed)
                {
                    ReportHub.LogError(ReportCategory.MULTIPLAYER, "Receiving emote stopped while disposed");
                    break;
                }

                if (!peerIdCache.TryGetWallet(emoteStopped.SubjectId, out string walletId))
                {
                    ReportHub.LogWarning(ReportCategory.MULTIPLAYER, $"Received EmoteStopped from unknown peer {emoteStopped.SubjectId}");
                    continue;
                }

                // TODO: Handle emote stop for remote players (e.g. set StopEmote flag)
                ReportHub.Log(ReportCategory.MULTIPLAYER, $"EmoteStopped for {walletId}, reason: {emoteStopped.Reason}");
            }
        }
    }
}
