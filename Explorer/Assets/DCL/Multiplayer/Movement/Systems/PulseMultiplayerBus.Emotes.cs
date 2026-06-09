using CommunicationData.URLHelpers;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Multiplayer.Connections.Pulse;
using DCL.Multiplayer.Emotes;
using DCL.Multiplayer.Profiles.Bunches;
using DCL.Optimization.Multithreading;
using DCL.Optimization.Pools;
using DCL.Web3;
using Decentraland.Pulse;
using Pulse.Transport;
using System.Collections.Generic;

namespace DCL.Multiplayer.Movement
{
    public partial class PulseMultiplayerBus
    {
        private readonly HashSet<uint> emotingSubjects = new ();
        private readonly HashSet<RemoteEmoteIntention> emoteIntentions = new (PoolConstants.AVATARS_COUNT);
        private readonly HashSet<RemoteEmoteStopIntention> emoteStopIntentions = new (PoolConstants.AVATARS_COUNT);
        private readonly MutexSync emoteSync = new ();

        public OwnedBunch<RemoteEmoteIntention> EmoteIntentions() =>
            new (emoteSync, emoteIntentions);

        public void Send(URN urn, bool loopCyclePassed, AvatarEmoteMask mask, uint durationMs = 0, NetworkMovementMessage? playerState = null)
        {
            if (loopCyclePassed)
                return;

            // Capture the local emote regardless of session state so a reconnect handshake can
            // backdate this emote via PlayerInitialState.EmoteStartOffsetMs.
            StoreLastEmoteStart(urn, durationMs, playerState);

            var outgoing = OutgoingMessage.Create(
                PacketMode.RELIABLE,
                ClientMessage.MessageOneofCase.EmoteStart);

            outgoing.Message.EmoteStart.EmoteId = urn;

            if (durationMs > 0)
                outgoing.Message.EmoteStart.DurationMs = durationMs;

            if (mask != AvatarEmoteMask.AemFullBody)
                outgoing.Message.EmoteStart.Mask = (int)mask;

            if (playerState.HasValue)
            {
                var playerStateInput = new PlayerStateInput();
                WritePlayerStateInput(playerState.Value, playerStateInput);
                outgoing.Message.EmoteStart.PlayerState = playerStateInput.State;
            }

            pulseService.Send(outgoing);
        }

        public void SendStop()
        {
            ClearLastEmote();

            var outgoing = OutgoingMessage.Create(
                PacketMode.RELIABLE,
                ClientMessage.MessageOneofCase.EmoteStop);

            pulseService.Send(outgoing);
        }

        public void OnPlayerRemoved(string walletId) { }

        public void SaveForRetry(RemoteEmoteIntention intention)
        {
            using (emoteSync.GetScope())
                emoteIntentions.Add(intention);
        }

        public OwnedBunch<RemoteEmoteStopIntention> EmoteStopIntentions() =>
            new (emoteSync, emoteStopIntentions);

        public void SaveForRetry(RemoteEmoteStopIntention intention)
        {
            using (emoteSync.GetScope())
                emoteStopIntentions.Add(intention);
        }

        private void EnqueueEmoteIntention(RemoteEmoteIntention intention)
        {
            using (emoteSync.GetScope())
                emoteIntentions.Add(intention);
        }

        private void HandleEmoteStarted(IncomingMessage message)
        {
            if (isDisposed)
            {
                ReportHub.LogError(ReportCategory.MULTIPLAYER, "Receiving emote started while disposed");
                return;
            }

            EmoteStarted emoteStarted = message.Message.EmoteStarted;

            if (!peerIdCache.TryGetWallet(emoteStarted.SubjectId, out Web3Address walletId))
            {
                ReportHub.LogWarning(ReportCategory.MULTIPLAYER, $"Received EmoteStarted from unknown peer {emoteStarted.SubjectId}");
                return;
            }

            emotingSubjects.Add(emoteStarted.SubjectId);

            NetworkMovementMessage movementMessage = ToNetworkMovementMessage(emoteStarted.PlayerState, emoteStarted.SubjectId, emoteStarted.ServerTick, isInstant: true);

            // if the snapshot on the server was evicted due to the high latency, EmoteStarted may arrive with the same sequence as Teleport or Diff
            // It's a valid case - override the snapshot
            TryUpdateLastMovementAndCompleteResync(emoteStarted.ServerTick, emoteStarted.SubjectId, emoteStarted.Sequence, movementMessage, true);

            // A delta with the same sequence may have arrived first via the unreliable channel,
            // storing isEmoting=false. EmoteStarted (reliable) is authoritative, so force-update the stored state.
            // Should be unreachable now
            if (lastMovementMessages.TryGetValue(emoteStarted.SubjectId, out (uint sequence, NetworkMovementMessage message) stored)
                && !stored.message.isEmoting)
            {
                stored.message.isEmoting = true;
                lastMovementMessages[emoteStarted.SubjectId] = stored;
                EmoteStateMismatchCount++;
            }

            Inbox(movementMessage, walletId);

            double timestamp = emoteStarted.ServerTick * SERVER_TICKS_TO_MOVEMENT_TIMESTAMP;

            EnqueueEmoteIntention(new RemoteEmoteIntention(new URN(emoteStarted.EmoteId), walletId, timestamp,
                emoteStarted.HasMask ? (AvatarEmoteMask)emoteStarted.Mask : AvatarEmoteMask.AemFullBody));
        }

        private void HandleEmoteStopped(IncomingMessage message)
        {
            if (isDisposed)
            {
                ReportHub.LogError(ReportCategory.MULTIPLAYER, "Receiving emote stopped while disposed");
                return;
            }

            EmoteStopped emoteStopped = message.Message.EmoteStopped;

            if (!peerIdCache.TryGetWallet(emoteStopped.SubjectId, out Web3Address walletId))
            {
                ReportHub.LogWarning(ReportCategory.MULTIPLAYER, $"Received EmoteStopped from unknown peer {emoteStopped.SubjectId}");
                return;
            }

            emotingSubjects.Remove(emoteStopped.SubjectId);

            double timestamp = emoteStopped.ServerTick * SERVER_TICKS_TO_MOVEMENT_TIMESTAMP;

            using (emoteSync.GetScope())
                emoteStopIntentions.Add(new RemoteEmoteStopIntention(walletId, timestamp));

            // It carries a new snapshot with the refreshed sequence
            if (emoteStopped.PlayerState != null)
            {
                NetworkMovementMessage movementMessage = ToNetworkMovementMessage(emoteStopped.PlayerState, emoteStopped.SubjectId, emoteStopped.ServerTick, false);
                TryUpdateLastMovementAndCompleteResync(emoteStopped.ServerTick, emoteStopped.SubjectId, emoteStopped.Sequence, movementMessage);
            }

            // Update the stored state so it will be applied with the next delta
            else if (lastMovementMessages.TryGetValue(emoteStopped.SubjectId, out (uint sequence, NetworkMovementMessage message) lastMessage))
            {
                lastMessage.message.isEmoting = false;
                lastMovementMessages[emoteStopped.SubjectId] = (lastMessage.sequence, lastMessage.message);
            }

            ReportHub.Log(ReportCategory.MULTIPLAYER, $"EmoteStopped for {walletId}, reason: {emoteStopped.Reason}");
        }

        internal bool IsPeerEmoting(Web3Address wallet) =>
            peerIdCache.TryGetPeerId(wallet, out uint subjectId)
            && emotingSubjects.Contains(subjectId);
    }
}
