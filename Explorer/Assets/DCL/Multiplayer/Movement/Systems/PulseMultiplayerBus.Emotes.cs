using CommunicationData.URLHelpers;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Multiplayer.Emotes;
using DCL.Multiplayer.Movement;
using DCL.Multiplayer.Profiles.Bunches;
using DCL.Optimization.Multithreading;
using DCL.Optimization.Pools;
using Decentraland.Pulse;
using Pulse.Transport;
using System;
using System.Collections.Generic;

namespace DCL.Multiplayer.Connections.Pulse
{
    public partial class PulseMultiplayerBus
    {
        private readonly HashSet<uint> emotingSubjects = new ();
        private readonly HashSet<RemoteEmoteIntention> emoteIntentions = new (PoolConstants.AVATARS_COUNT);
        private readonly MutexSync emoteSync = new ();

        public OwnedBunch<RemoteEmoteIntention> EmoteIntentions() =>
            new (emoteSync, emoteIntentions);

        public void Send(URN urn, bool loopCyclePassed, AvatarEmoteMask mask, uint durationMs = 0, NetworkMovementMessage? playerState = null)
        {
            if (loopCyclePassed)
                return;

            var outgoing = OutgoingMessage.Create(
                PacketMode.RELIABLE,
                ClientMessage.MessageOneofCase.EmoteStart);

            outgoing.Message.EmoteStart.EmoteId = urn;

            if (durationMs > 0)
                outgoing.Message.EmoteStart.DurationMs = durationMs;

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
            throw new NotImplementedException();

        public void SaveForRetry(RemoteEmoteStopIntention intention)
        {
            throw new NotImplementedException();
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

                // A delta with the same sequence may have arrived first via the unreliable channel,
                // storing isEmoting=false. EmoteStarted (reliable) is authoritative, so force-update the stored state.
                if (lastMovementMessages.TryGetValue(emoteStarted.SubjectId, out (uint sequence, NetworkMovementMessage message) stored)
                    && !stored.message.isEmoting)
                {
                    stored.message.isEmoting = true;
                    lastMovementMessages[emoteStarted.SubjectId] = stored;
                    EmoteStateMismatchCount++;
                }

                Inbox(movementMessage, walletId);
            }

            float timestamp = emoteStarted.ServerTick * SERVER_TICKS_TO_MOVEMENT_TIMESTAMP;
            EnqueueEmoteIntention(new RemoteEmoteIntention(new URN(emoteStarted.EmoteId), walletId, timestamp, AvatarEmoteMask.AemFullBody));
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
