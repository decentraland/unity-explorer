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
using System;
using System.Collections.Generic;

namespace DCL.Multiplayer.Movement
{
    public partial class PulseMultiplayerBus
    {
        private const string EMOTE_STARTED_MESSAGE = "EmoteStarted";
        private const string EMOTE_STOPPED_MESSAGE = "EmoteStopped";

        private readonly Dictionary<uint, (uint sequence, URN urn, AvatarEmoteMask mask, bool active)> peerEmotes = new ();
        private readonly HashSet<RemoteEmoteIntention> emoteIntentions = new (PoolConstants.AVATARS_COUNT);
        private readonly HashSet<RemoteEmoteStopIntention> emoteStopIntentions = new (PoolConstants.AVATARS_COUNT);
        private readonly Dictionary<string, ulong> emoteRetryGenerations = new (StringComparer.OrdinalIgnoreCase);
        private readonly MutexSync emoteSync = new ();
        private ulong nextEmoteRetryGeneration;

        public OwnedBunch<RemoteEmoteIntention> EmoteIntentions() =>
            new (emoteSync, emoteIntentions);

        public void Send(URN urn, bool loopCyclePassed, AvatarEmoteMask mask, uint durationMs = 0, NetworkMovementMessage? playerState = null)
        {
            if (loopCyclePassed)
                return;

            // Capture the local emote regardless of session state so a reconnect handshake can
            // backdate this emote via PlayerInitialState.EmoteStartOffsetMs.
            StoreLastEmoteStart(urn, durationMs, playerState, mask);

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
                if (!isDisposed && ReferenceEquals(intention.Source, this)
                    && emoteRetryGenerations.TryGetValue(intention.WalletId, out ulong generation)
                    && generation == intention.RetryGeneration)
                    emoteIntentions.Add(intention);
        }

        public OwnedBunch<RemoteEmoteStopIntention> EmoteStopIntentions() =>
            new (emoteSync, emoteStopIntentions);

        public void SaveForRetry(RemoteEmoteStopIntention intention)
        {
            using (emoteSync.GetScope())
                if (!isDisposed && ReferenceEquals(intention.Source, this)
                    && emoteRetryGenerations.TryGetValue(intention.WalletId, out ulong generation)
                    && generation == intention.RetryGeneration)
                    emoteStopIntentions.Add(intention);
        }

        private void EnqueueEmoteIntention(URN emoteId, string wallet, double timestamp, AvatarEmoteMask mask)
        {
            using (emoteSync.GetScope())
            {
                ulong generation = AdvanceEmoteRetryGeneration(wallet);
                emoteIntentions.Add(new RemoteEmoteIntention(emoteId, wallet, timestamp, mask, this, generation));
            }
        }

        private void ClearWalletEmoteIntentions(string wallet)
        {
            emoteIntentions.RemoveWhere(intention => string.Equals(intention.WalletId, wallet, StringComparison.OrdinalIgnoreCase));
            emoteStopIntentions.RemoveWhere(intention => string.Equals(intention.WalletId, wallet, StringComparison.OrdinalIgnoreCase));
            emoteRetryGenerations.Remove(wallet);
        }

        private ulong AdvanceEmoteRetryGeneration(string wallet)
        {
            ClearWalletEmoteIntentions(wallet);
            ulong generation = ++nextEmoteRetryGeneration;
            emoteRetryGenerations[wallet] = generation;
            return generation;
        }

        private void ClearEmoteIntentions()
        {
            using (emoteSync.GetScope())
            {
                emoteIntentions.Clear();
                emoteStopIntentions.Clear();
                emoteRetryGenerations.Clear();
            }
        }

        private void RestorePeerEmote(Web3Address wallet, uint subjectId, double timestamp, bool stopIfInactive = true)
        {
            using (emoteSync.GetScope())
            {
                if (peerEmotes.TryGetValue(subjectId, out var activeEmote) && activeEmote.active)
                {
                    ulong generation = AdvanceEmoteRetryGeneration(wallet);
                    emoteIntentions.Add(new RemoteEmoteIntention(activeEmote.urn, wallet, timestamp, activeEmote.mask, this, generation));
                }
                else if (stopIfInactive)
                {
                    ulong generation = AdvanceEmoteRetryGeneration(wallet);
                    emoteStopIntentions.Add(new RemoteEmoteStopIntention(wallet, timestamp, this, generation));
                }
                else
                    ClearWalletEmoteIntentions(wallet);
            }
        }

        private void HandleEmoteStarted(IncomingMessage message)
        {
            if (isDisposed)
            {
                ReportHub.LogError(ReportCategory.MULTIPLAYER, "Receiving emote started while disposed");
                return;
            }

            EmoteStarted emoteStarted = message.Message.EmoteStarted;

            PeerIdCache.LookupResult lookup = GetPeerInCurrentRealm(emoteStarted.SubjectId, EMOTE_STARTED_MESSAGE, out Web3Address walletId);
            if (lookup is not (PeerIdCache.LookupResult.Found or PeerIdCache.LookupResult.SupersededPeer))
                return;

            bool isShadow = lookup == PeerIdCache.LookupResult.SupersededPeer;
            if (peerEmotes.TryGetValue(emoteStarted.SubjectId, out var previousEmote)
                && (emoteStarted.Sequence < previousEmote.sequence || (emoteStarted.Sequence == previousEmote.sequence && !previousEmote.active)))
                return;

            var emoteId = new URN(emoteStarted.EmoteId);
            AvatarEmoteMask mask = emoteStarted.HasMask ? (AvatarEmoteMask)emoteStarted.Mask : AvatarEmoteMask.AemFullBody;
            peerEmotes[emoteStarted.SubjectId] = (emoteStarted.Sequence, emoteId, mask, true);

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
                emoteStateMismatchCount++;
            }

            if (isShadow)
                return;

            Inbox(movementMessage, walletId);

            double timestamp = emoteStarted.ServerTick * SERVER_TICKS_TO_MOVEMENT_TIMESTAMP;

            EnqueueEmoteIntention(emoteId, walletId, timestamp, mask);
        }

        private void HandleEmoteStopped(IncomingMessage message)
        {
            if (isDisposed)
            {
                ReportHub.LogError(ReportCategory.MULTIPLAYER, "Receiving emote stopped while disposed");
                return;
            }

            EmoteStopped emoteStopped = message.Message.EmoteStopped;

            PeerIdCache.LookupResult lookup = GetPeerInCurrentRealm(emoteStopped.SubjectId, EMOTE_STOPPED_MESSAGE, out Web3Address walletId);
            if (lookup is not (PeerIdCache.LookupResult.Found or PeerIdCache.LookupResult.SupersededPeer))
                return;

            bool isShadow = lookup == PeerIdCache.LookupResult.SupersededPeer;
            if (peerEmotes.TryGetValue(emoteStopped.SubjectId, out var previousEmote) && emoteStopped.Sequence < previousEmote.sequence)
                return;

            peerEmotes[emoteStopped.SubjectId] = (emoteStopped.Sequence, default(URN), AvatarEmoteMask.AemFullBody, false);

            if (emoteStopped.PlayerState != null)
            {
                NetworkMovementMessage movementMessage = ToNetworkMovementMessage(emoteStopped.PlayerState, emoteStopped.SubjectId, emoteStopped.ServerTick, false);
                TryUpdateLastMovementAndCompleteResync(emoteStopped.ServerTick, emoteStopped.SubjectId, emoteStopped.Sequence, movementMessage, allowOverrides: true);
            }

            // A stop can follow a newer pose. Clear its emote flag without replacing the pose or baseline sequence.
            if (lastMovementMessages.TryGetValue(emoteStopped.SubjectId, out (uint sequence, NetworkMovementMessage message) lastMessage))
            {
                lastMessage.message.isEmoting = false;
                lastMovementMessages[emoteStopped.SubjectId] = (lastMessage.sequence, lastMessage.message);
            }

            if (isShadow)
                return;

            double timestamp = emoteStopped.ServerTick * SERVER_TICKS_TO_MOVEMENT_TIMESTAMP;

            using (emoteSync.GetScope())
            {
                ulong generation = AdvanceEmoteRetryGeneration(walletId);
                emoteStopIntentions.Add(new RemoteEmoteStopIntention(walletId, timestamp, this, generation));
            }

            ReportHub.Log(ReportCategory.MULTIPLAYER, $"EmoteStopped for {walletId}, reason: {emoteStopped.Reason}");
        }

        internal bool IsPeerEmoting(Web3Address wallet) =>
            peerIdCache.TryGetPeerId(wallet, out uint subjectId)
            && IsSubjectEmoting(subjectId);

        private bool IsSubjectEmoting(uint subjectId) =>
            peerEmotes.TryGetValue(subjectId, out var emote) && emote.active;
    }
}
