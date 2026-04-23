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

        private readonly HashSet<string> previousActiveSpeakers = new ();
        private readonly HashSet<string> currentActiveSpeakers = new ();

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
            if (state is NearbyVoiceChatState.SPEAKING)
            {
                string localPlayer = islandRoom.Participants.LocalParticipant().Identity;
                bool isSpeaking = !string.IsNullOrEmpty(localPlayer) && islandRoom.ActiveSpeakers.Contains(localPlayer);
                world.AddOrSet(playerEntity, new VoiceChatNametagComponent(isSpeaking: isSpeaking));
            }
            else
                world.AddOrSet(playerEntity, new VoiceChatNametagComponent(isSpeaking: false) { IsRemoving = true });
        }

        private void OnActiveSpeakersUpdated()
        {
            string localPlayer = islandRoom.Participants.LocalParticipant().Identity;
            bool localInSpeakingState = nearbyStateModel.State.Value == NearbyVoiceChatState.SPEAKING;

            currentActiveSpeakers.Clear();

            foreach (string? identity in islandRoom.ActiveSpeakers)
                if (!string.IsNullOrEmpty(identity))
                    currentActiveSpeakers.Add(identity);

            // Local: update IsSpeaking only while publishing (State==SPEAKING). Leaving SPEAKING is handled by ApplyLocalPublishingFromState.
            if (localInSpeakingState && !string.IsNullOrEmpty(localPlayer))
                world.AddOrSet(playerEntity, new VoiceChatNametagComponent(isSpeaking: currentActiveSpeakers.Contains(localPlayer)));

            // Speakers who stopped speaking: flip IsSpeaking to false (dots). Component stays until TrackUnsubscribed removes it.
            foreach (string identity in previousActiveSpeakers)
            {
                if (currentActiveSpeakers.Contains(identity)) continue;
                if (identity == localPlayer) continue;

                if (entityParticipantTable.TryGet(identity, out IReadOnlyEntityParticipantTable.Entry entry)
                    && world.Has<VoiceChatNametagComponent>(entry.Entity))
                    world.AddOrSet(entry.Entity, new VoiceChatNametagComponent(isSpeaking: false));
            }

            // Speakers who started speaking: flip IsSpeaking to true (wave).
            foreach (string identity in currentActiveSpeakers)
            {
                if (identity == localPlayer) continue;

                if (entityParticipantTable.TryGet(identity, out IReadOnlyEntityParticipantTable.Entry entry))
                    world.AddOrSet(entry.Entity, new VoiceChatNametagComponent(isSpeaking: true));
            }

            previousActiveSpeakers.Clear();
            previousActiveSpeakers.UnionWith(currentActiveSpeakers);
        }
    }
}
