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
        ///     Delta doesn't carry the "Emoting" state. RELIABLE and UNRELIABLE_SEQUENCED share the same
        ///     ENet channel, so each reliable packet (EmoteStarted, EmoteStopped) acts as an ordering barrier:
        ///     a STATE_DELTA sent after the reliable packet cannot arrive before it.
        ///     <para />
        ///     The <c>isEmoting</c> flag in <c>lastMovementMessages</c> is patched explicitly in
        ///     <see cref="HandleEmoteStopped" /> because <c>MergeIntoNetworkMovementMessage</c> does not
        ///     recompute it — without the patch, merged deltas would carry stale <c>isEmoting=true</c>.
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
