using Arch.Core;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Utilities;
using LiveKit.Proto;
using LiveKit.Rooms;
using LiveKit.Rooms.Participants;
using LiveKit.Rooms.TrackPublications;
using LiveKit.Rooms.Tracks;
using System;
using Utility.Arch;

namespace DCL.VoiceChat
{
    public class VoiceChatNametagsHandler : IDisposable
    {
        private readonly IRoom room;
        private readonly ActiveSpeakersDiffTracker tracker;
        private readonly IReadonlyReactiveProperty<VoiceChatActivityState> activityState;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
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
            Entity playerEntity)
        {
            this.room = room;
            this.activityState = activityState;
            this.entityParticipantTable = entityParticipantTable;
            this.world = world;
            this.playerEntity = playerEntity;

            tracker = new ActiveSpeakersDiffTracker(entityParticipantTable, world);

            statusSubscription = activityState.Subscribe(OnActivityStateChanged);
            room.ActiveSpeakers.Updated += OnActiveSpeakersUpdated;
            room.Participants.UpdatesFromParticipant += OnParticipantUpdated;
            room.TrackUnsubscribed += OnTrackUnsubscribed;
            entityParticipantTable.OnRegistered += OnEntityRegistered;

            // Subscribe does not fire for the current value, so sync existing remote speakers
            // if voice chat is already active when the handler is created.
            // Only tracker is updated — LocalParticipant may not be initialized at construction time.
            if (activityState.Value == VoiceChatActivityState.ACTIVE)
                tracker.Update(room.ActiveSpeakers);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            statusSubscription?.Dispose();
            room.Participants.UpdatesFromParticipant -= OnParticipantUpdated;
            room.ActiveSpeakers.Updated -= OnActiveSpeakersUpdated;
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
                    OnActiveSpeakersUpdated();
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
            UpdateLocalPlayerSpeakingState();
        }

        private void OnEntityRegistered(string walletId)
        {
            if (activityState.Value != VoiceChatActivityState.ACTIVE) return;

            tracker.RetrySpeaker(walletId);
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
            string localIdentity = room.Participants.LocalParticipant().Identity;
            bool isSpeaking = IsIdentityAmongActiveSpeakers(localIdentity);

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

        private void MarkLocalPlayerRemoving()
        {
            localPlayerSpeaking = false;
            world.AddOrSet(playerEntity, new VoiceChatNametagComponent(false) { IsRemoving = true });
        }
    }
}
