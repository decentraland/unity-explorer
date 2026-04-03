using CommunicationData.URLHelpers;
using DCL.Diagnostics;
using DCL.Multiplayer.Emotes;
using DCL.Multiplayer.Movement;
using Decentraland.Pulse;

namespace DCL.Multiplayer.Connections.Pulse
{
    public partial class PulseMultiplayerBus
    {
        private void HandleEmoteStarted(IncomingMessage message)
        {
            if (isDisposed)
            {
                ReportHub.LogError(ReportCategory.MULTIPLAYER, "Receiving emote started while disposed");
                return;
            }

            EmoteStarted emoteStarted = message.Message.EmoteStarted;

            if (!peerIdCache.TryGetWallet(emoteStarted.SubjectId, out string walletId))
            {
                ReportHub.LogWarning(ReportCategory.MULTIPLAYER, $"Received EmoteStarted from unknown peer {emoteStarted.SubjectId}");
                return;
            }

            if (emoteStarted.PlayerState != null)
            {
                NetworkMovementMessage movementMessage = ToNetworkMovementMessage(emoteStarted.PlayerState, emoteStarted.ServerTick, isInstant: true, isEmoting: true);
                Inbox(movementMessage, walletId);
            }

            double timestamp = emoteStarted.ServerTick * SERVER_TICKS_TO_MOVEMENT_TIMESTAMP;
            emotesMessageBus.Enqueue(new RemoteEmoteIntention(new URN(emoteStarted.EmoteId), walletId, timestamp));
        }

        private void HandleEmoteStopped(IncomingMessage message)
        {
            if (isDisposed)
            {
                ReportHub.LogError(ReportCategory.MULTIPLAYER, "Receiving emote stopped while disposed");
                return;
            }

            EmoteStopped emoteStopped = message.Message.EmoteStopped;

            if (!peerIdCache.TryGetWallet(emoteStopped.SubjectId, out string walletId))
            {
                ReportHub.LogWarning(ReportCategory.MULTIPLAYER, $"Received EmoteStopped from unknown peer {emoteStopped.SubjectId}");
                return;
            }

            // TODO: Handle emote stop for remote players (e.g. set StopEmote flag)
            ReportHub.Log(ReportCategory.MULTIPLAYER, $"EmoteStopped for {walletId}, reason: {emoteStopped.Reason}");
        }
    }
}
