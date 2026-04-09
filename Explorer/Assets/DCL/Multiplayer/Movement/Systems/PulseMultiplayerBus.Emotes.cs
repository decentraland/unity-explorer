using CommunicationData.URLHelpers;
using DCL.Diagnostics;
using DCL.Multiplayer.Emotes;
using DCL.Multiplayer.Movement;
using Decentraland.Pulse;
using System.Collections.Generic;

namespace DCL.Multiplayer.Connections.Pulse
{
    public partial class PulseMultiplayerBus
    {
        /// <summary>
        ///     Delta doesn't carry the "Emoting" state (and make it to do so seems strange in the authoritative server domain)
        ///     Simple linear handling of EmoteStarted / EmoteStopped brings with it the following intricacies
        ///     as Delta is received from the different unreliable channel and is not synced with the reliable one:
        ///     - Delta (after emote has started) received before `EmoteStarted` - NO RISK - `EmoteStarted` carries a full snapshot or synchronization
        ///     - Delta (after emote has finished) received before `EmoteStopped` - RISK - Delta will be resolved as `IsEmoting: true` leading to the slight drift while emoting until `EmoteStopped` has been received
        ///     If during the test we notice a considerable drift, add one more flag to `PlayerAnimationFlags`
        /// </summary>
        private readonly HashSet<uint> emotingSubjects = new ();

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

            emotingSubjects.Add(emoteStarted.SubjectId);

            if (emoteStarted.PlayerState != null)
            {
                NetworkMovementMessage movementMessage = ToNetworkMovementMessage(emoteStarted.PlayerState, emoteStarted.SubjectId, emoteStarted.ServerTick, isInstant: true);
                TryUpdateLastMovementAndCompleteResync(emoteStarted.ServerTick, emoteStarted.SubjectId, emoteStarted.Sequence, movementMessage);

                // EmoteStarted is mutually exclusive with other messages so we don't receive two messages with the same Sequence
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

            emotingSubjects.Remove(emoteStopped.SubjectId);

            // Update the stored state so it will be applied with the next delta
            if (lastMovementMessages.TryGetValue(emoteStopped.SubjectId, out (uint sequence, NetworkMovementMessage message) lastMessage))
            {
                lastMessage.message.isEmoting = false;
                lastMovementMessages[emoteStopped.SubjectId] = (lastMessage.sequence, lastMessage.message);
            }

            ReportHub.Log(ReportCategory.MULTIPLAYER, $"EmoteStopped for {walletId}, reason: {emoteStopped.Reason}");
        }
    }
}
