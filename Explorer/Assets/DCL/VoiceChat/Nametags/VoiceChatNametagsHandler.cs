using Arch.Core;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Utilities;
using LiveKit.Proto;
using LiveKit.Rooms;
using LiveKit.Rooms.Participants;
using LiveKit.Rooms.TrackPublications;
using LiveKit.Rooms.Tracks;
using System;
using System.Collections.Generic;
using Utility.Arch;

namespace DCL.VoiceChat
{
    public class VoiceChatNametagsHandler : IDisposable
    {
        private readonly IRoom room;
        private readonly ActiveSpeakersDiffTracker tracker;
        private readonly IReadonlyReactiveProperty<VoiceChatActivityState> activityState;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly Func<string, bool>? isMuted;
        private readonly string? localIdentity;
        private readonly IDisposable statusSubscription;

        private readonly World world;
        private readonly Entity playerEntity;

        private bool disposed;
        private bool localPlayerSpeaking;

        public VoiceChatNametagsHandler(
            IRoom room,
            IReadonlyReactiveProperty<VoiceChatActivityState> activityState,
            IReadOnlyEntityParticipantTable entityParticipantTable,
            World world,
            Entity playerEntity,
            Func<string, bool>? isMuted = null,
            string? localIdentity = null)
        {
            this.room = room;
            this.activityState = activityState;
            this.entityParticipantTable = entityParticipantTable;
            this.world = world;
            this.playerEntity = playerEntity;
            this.isMuted = isMuted;
            this.localIdentity = localIdentity;

            tracker = new ActiveSpeakersDiffTracker(entityParticipantTable, world);

            statusSubscription = activityState.Subscribe(OnActivityStateChanged);
            room.ActiveSpeakers.Updated += OnActiveSpeakersUpdated;
            room.Participants.UpdatesFromParticipant += OnParticipantUpdated;
            room.TrackSubscribed += OnTrackSubscribed;
            room.TrackUnsubscribed += OnTrackUnsubscribed;
            entityParticipantTable.OnRegistered += OnEntityRegistered;

            // ActiveSpeakers does not include initial state on connect — only fires on changes.
            // Bootstrap from existing published audio tracks to cover already-speaking participants.
            if (activityState.Value == VoiceChatActivityState.ACTIVE)
                BootstrapExistingPublishers();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            statusSubscription?.Dispose();
            room.Participants.UpdatesFromParticipant -= OnParticipantUpdated;
            room.ActiveSpeakers.Updated -= OnActiveSpeakersUpdated;
            room.TrackSubscribed -= OnTrackSubscribed;
            room.TrackUnsubscribed -= OnTrackUnsubscribed;
            entityParticipantTable.OnRegistered -= OnEntityRegistered;

            (activityState as IDisposable)?.Dispose();

            tracker.MarkAllRemoving();
            MarkLocalPlayerRemoving();
        }

        private void OnActivityStateChanged(VoiceChatActivityState state)
        {
            switch (state)
            {
                case VoiceChatActivityState.ACTIVE:
                    // Process current ActiveSpeakers first (may be empty on fresh connect),
                    // then bootstrap from existing published tracks. Order matters:
                    // BootstrapExistingPublishers calls ForceStartSpeaking which adds to activeSpeakers,
                    // so it must run AFTER Update() to avoid being immediately undone by an empty diff.
                    OnActiveSpeakersUpdated();
                    BootstrapExistingPublishers();
                    break;
                case VoiceChatActivityState.INACTIVE:
                    tracker.MarkAllRemoving();
                    MarkLocalPlayerRemoving();
                    break;
            }
        }

        private void OnActiveSpeakersUpdated()
        {
            if (activityState.Value != VoiceChatActivityState.ACTIVE) return;

            tracker.Update(room.ActiveSpeakers);
            ApplyHushedStateToActiveSpeakers();
            UpdateLocalPlayerSpeakingState();
        }

        private void OnEntityRegistered(string walletId)
        {
            if (activityState.Value != VoiceChatActivityState.ACTIVE) return;

            tracker.RetrySpeaker(walletId);

            // RetrySpeaker only retries the speaking component. If the entity wasn't
            // registered when ActivateRemoteSpeaker ran, the hushed check also failed.
            // Apply it now that the entity exists.
            if (isMuted != null && isMuted(walletId)
                && entityParticipantTable.TryGet(walletId, out IReadOnlyEntityParticipantTable.Entry entry)
                && world.Has<VoiceChatNametagComponent>(entry.Entity))
                world.AddOrSet(entry.Entity, new VoiceChatHushedComponent());
        }

        private void OnTrackSubscribed(ITrack track, TrackPublication publication, LKParticipant participant)
        {
            if (publication.Kind != TrackKind.KindAudio) return;
            if (activityState.Value != VoiceChatActivityState.ACTIVE) return;
            if (participant.Identity == localIdentity) return;

            ActivateRemoteSpeaker(participant.Identity);
        }

        private void OnTrackUnsubscribed(ITrack track, TrackPublication publication, LKParticipant participant)
        {
            if (publication.Kind == TrackKind.KindAudio)
                tracker.ForceStopSpeaking(participant.Identity);
        }

        private void OnParticipantUpdated(LKParticipant participant, UpdateFromParticipant update)
        {
            if (update == UpdateFromParticipant.Disconnected)
                tracker.RemoveParticipant(participant.Identity);
        }

        private void UpdateLocalPlayerSpeakingState()
        {
            string identity = localIdentity ?? room.Participants.LocalParticipant().Identity;
            bool isSpeaking = IsIdentityAmongActiveSpeakers(identity);

            if (isSpeaking == localPlayerSpeaking)
                return;

            localPlayerSpeaking = isSpeaking;
            world.AddOrSet(playerEntity, new VoiceChatNametagComponent(isSpeaking));
        }

        private bool IsIdentityAmongActiveSpeakers(string identity)
        {
            foreach (string speakerId in room.ActiveSpeakers)
            {
                if (speakerId == identity)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Mirrors <see cref="RemoteTrackListener.StartListeningAsync"/> — iterates existing
        /// remote participants and force-starts nametags for those with published audio tracks.
        /// Needed because ActiveSpeakers does not include initial state on connect.
        /// </summary>
        private void BootstrapExistingPublishers()
        {
            foreach (KeyValuePair<string, LKParticipant> kvp in room.Participants.RemoteParticipantIdentities())
            {
                foreach ((string _, TrackPublication publication) in kvp.Value.Tracks)
                {
                    if (publication.Kind != TrackKind.KindAudio) continue;

                    ActivateRemoteSpeaker(kvp.Key);
                    break;
                }
            }
        }

        private void ActivateRemoteSpeaker(string participantId)
        {
            tracker.ForceStartSpeaking(participantId);

            if (isMuted != null && isMuted(participantId)
                && entityParticipantTable.TryGet(participantId, out IReadOnlyEntityParticipantTable.Entry entry))
                world.AddOrSet(entry.Entity, new VoiceChatHushedComponent());
        }

        private void ApplyHushedStateToActiveSpeakers()
        {
            if (isMuted == null) return;

            foreach (string speakerId in room.ActiveSpeakers)
            {
                if (!isMuted(speakerId)) continue;
                if (!entityParticipantTable.TryGet(speakerId, out IReadOnlyEntityParticipantTable.Entry entry)) continue;

                world.AddOrSet(entry.Entity, new VoiceChatHushedComponent());
            }
        }

        private void MarkLocalPlayerRemoving()
        {
            localPlayerSpeaking = false;
            world.AddOrSet(playerEntity, new VoiceChatNametagComponent(false) { IsRemoving = true });
        }
    }
}
