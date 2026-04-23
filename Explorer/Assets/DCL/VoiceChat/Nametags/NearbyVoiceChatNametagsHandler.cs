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

namespace DCL.VoiceChat.Nearby
{
    /// <summary>
    ///     Drives <see cref="VoiceChatNametagComponent"/> for every participant currently publishing
    ///     audio in the Island Room, so nametags can render the sound-wave indicator for everyone
    ///     connected to nearby voice. <see cref="VoiceChatNametagComponent.IsSpeaking"/> toggles between
    ///     the animated wave (identity is in <see cref="LiveKit.Rooms.ActiveSpeakers.IActiveSpeakers"/>)
    ///     and the idle dots (publishing, but silent).
    /// </summary>
    public class NearbyVoiceChatNametagsHandler : IDisposable
    {
        private readonly IRoom islandRoom;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly World world;
        private readonly Entity playerEntity;
        private readonly NearbyVoiceChatStateModel nearbyStateModel;
        private readonly IDisposable stateSubscription;

        private bool disposed;

        public NearbyVoiceChatNametagsHandler(
            IRoom islandRoom,
            IReadOnlyEntityParticipantTable entityParticipantTable,
            World world,
            Entity playerEntity,
            NearbyVoiceChatStateModel nearbyStateModel)
        {
            this.islandRoom = islandRoom;
            this.entityParticipantTable = entityParticipantTable;
            this.world = world;
            this.playerEntity = playerEntity;
            this.nearbyStateModel = nearbyStateModel;

            islandRoom.TrackSubscribed += OnTrackSubscribed;
            islandRoom.TrackUnsubscribed += OnTrackUnsubscribed;
            islandRoom.ActiveSpeakers.Updated += OnActiveSpeakersUpdated;

            stateSubscription = nearbyStateModel.State.Subscribe(ApplyLocalPublishingFromState);
            // ApplyLocalPublishingFromState(nearbyStateModel.State.Value);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            islandRoom.TrackSubscribed -= OnTrackSubscribed;
            islandRoom.TrackUnsubscribed -= OnTrackUnsubscribed;
            islandRoom.ActiveSpeakers.Updated -= OnActiveSpeakersUpdated;

            stateSubscription.Dispose();
        }

        // Nametags Placement
        private void OnTrackSubscribed(ITrack track, TrackPublication publication, LKParticipant participant)
        {
            if (publication.Kind == TrackKind.KindAudio && entityParticipantTable.TryGet(participant.Identity, out IReadOnlyEntityParticipantTable.Entry entry))
                world.AddOrSet(entry.Entity, new VoiceChatNametagComponent(isSpeaking: islandRoom.ActiveSpeakers.Contains(participant.Identity)));
        }

        private void OnTrackUnsubscribed(ITrack track, TrackPublication publication, LKParticipant participant)
        {
            if (publication.Kind == TrackKind.KindAudio && entityParticipantTable.TryGet(participant.Identity, out IReadOnlyEntityParticipantTable.Entry entry))
                world.AddOrSet(entry.Entity, new VoiceChatNametagComponent(isSpeaking: false) { IsRemoving = true });
        }

        private void ApplyLocalPublishingFromState(NearbyVoiceChatState state)
        {
            world.AddOrSet(playerEntity, state is NearbyVoiceChatState.SPEAKING
                ? new VoiceChatNametagComponent(isSpeaking: nearbyStateModel.IsLocalSpeaking)
                : new VoiceChatNametagComponent(isSpeaking: false) { IsRemoving = true });
        }

        private void OnActiveSpeakersUpdated()
        {
            // Update local
            string localPlayer = islandRoom.Participants.LocalParticipant().Identity;
            bool isLocalActive = !string.IsNullOrEmpty(localPlayer) && islandRoom.ActiveSpeakers.Contains(localPlayer)
                                                                    && nearbyStateModel.State.Value == NearbyVoiceChatState.SPEAKING;

            world.AddOrSet(playerEntity, isLocalActive
                ? new VoiceChatNametagComponent(isSpeaking: nearbyStateModel.IsLocalSpeaking)
                : new VoiceChatNametagComponent(isSpeaking: false) { IsRemoving = true });

            foreach (string? identity in islandRoom.ActiveSpeakers)
            {
                if (entityParticipantTable.TryGet(identity, out IReadOnlyEntityParticipantTable.Entry entry) && identity != localPlayer)
                    world.AddOrSet(entry.Entity, new VoiceChatNametagComponent(true));
            }

            // set not speaking others
        }
    }
}
