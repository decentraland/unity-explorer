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
    ///     Drives <see cref="VoiceChatNametagComponent"/> for every participant currently publishing audio in the Island Room,
    ///     so nametags can render the sound-wave indicator for everyone connected to nearby voice.
    ///     <see cref="VoiceChatNametagComponent.IsSpeaking"/> toggles between the animated wave and the idle dots (publishing, but silent).
    /// </summary>
    public class NearbyVoiceChatNametagsHandler : IDisposable
    {
        private static readonly QueryDescription NAMETAGS_QUERY = new QueryDescription().WithAll<VoiceChatNametagComponent>();

        private readonly IRoom islandRoom;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly World world;
        private readonly Entity playerEntity;
        private readonly NearbyVoiceChatStateModel nearbyStateModel;
        private readonly NearbyMuteService muteService;
        private readonly IDisposable stateSubscription;

        private readonly HashSet<string> previousActiveSpeakers = new ();
        private readonly HashSet<string> currentActiveSpeakers = new ();

        private bool disposed;

        public NearbyVoiceChatNametagsHandler(
            IRoom islandRoom,
            IReadOnlyEntityParticipantTable entityParticipantTable,
            World world,
            Entity playerEntity,
            NearbyVoiceChatStateModel nearbyStateModel,
            NearbyMuteService muteService)
        {
            this.islandRoom = islandRoom;
            this.entityParticipantTable = entityParticipantTable;
            this.world = world;
            this.playerEntity = playerEntity;
            this.nearbyStateModel = nearbyStateModel;
            this.muteService = muteService;

            islandRoom.TrackSubscribed += OnTrackSubscribed;
            islandRoom.TrackUnsubscribed += OnTrackUnsubscribed;
            islandRoom.ActiveSpeakers.Updated += OnActiveSpeakersUpdated;
            muteService.MuteStateChanged += OnMuteStateChanged;

            stateSubscription = nearbyStateModel.State.Subscribe(OnStateChanged);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            islandRoom.TrackSubscribed -= OnTrackSubscribed;
            islandRoom.TrackUnsubscribed -= OnTrackUnsubscribed;
            islandRoom.ActiveSpeakers.Updated -= OnActiveSpeakersUpdated;
            muteService.MuteStateChanged -= OnMuteStateChanged;

            stateSubscription.Dispose();
        }

        // Nametags Placement
        private void OnTrackSubscribed(ITrack track, TrackPublication publication, LKParticipant participant)
        {
            if (publication.Kind != TrackKind.KindAudio) return;
            if (!IsNearbyActive()) return;
            if (!entityParticipantTable.TryGet(participant.Identity, out IReadOnlyEntityParticipantTable.Entry entry)) return;

            world.AddOrSet(entry.Entity, new VoiceChatNametagComponent(
                isSpeaking: islandRoom.ActiveSpeakers.Contains(participant.Identity),
                type: VoiceChatType.NEARBY,
                isHushed: muteService.IsMuted(participant.Identity)));
        }

        private void OnTrackUnsubscribed(ITrack track, TrackPublication publication, LKParticipant participant)
        {
            if (publication.Kind != TrackKind.KindAudio) return;

            if (entityParticipantTable.TryGet(participant.Identity, out IReadOnlyEntityParticipantTable.Entry entry))
                world.AddOrSet(entry.Entity, new VoiceChatNametagComponent(isSpeaking: false, type: VoiceChatType.NEARBY) { IsRemoving = true });
        }

        private void OnMuteStateChanged(string walletId, bool isMuted)
        {
            if (!IsNearbyActive()) return;
            if (!entityParticipantTable.TryGet(walletId, out IReadOnlyEntityParticipantTable.Entry entry)) return;
            if (!world.Has<VoiceChatNametagComponent>(entry.Entity)) return;

            world.AddOrSet(entry.Entity, new VoiceChatNametagComponent(
                isSpeaking: islandRoom.ActiveSpeakers.Contains(walletId),
                type: VoiceChatType.NEARBY,
                isHushed: isMuted));
        }

        private void OnStateChanged(NearbyVoiceChatState state)
        {
            switch (state)
            {
                case NearbyVoiceChatState.DISABLED:
                case NearbyVoiceChatState.SUPPRESSED:
                    ClearAllIndicators();
                    break;

                case NearbyVoiceChatState.IDLE:
                    // Local stops publishing; remote indicators are (re)driven by Track events + ActiveSpeakers.
                    world.AddOrSet(playerEntity, new VoiceChatNametagComponent(isSpeaking: false, type: VoiceChatType.NEARBY) { IsRemoving = true });
                    break;

                case NearbyVoiceChatState.SPEAKING:
                    string localPlayer = islandRoom.Participants.LocalParticipant().Identity;
                    bool isSpeaking = !string.IsNullOrEmpty(localPlayer) && islandRoom.ActiveSpeakers.Contains(localPlayer);
                    world.AddOrSet(playerEntity, new VoiceChatNametagComponent(isSpeaking: isSpeaking, type: VoiceChatType.NEARBY));
                    break;
            }
        }

        private void ClearAllIndicators()
        {
            // Bulk pass: only entities still owned by nearby (Type == NEARBY) get cleared.
            // Private/community handler overwrites the component with its own Type, so this pass skips them.
            world.Query(in NAMETAGS_QUERY, (ref VoiceChatNametagComponent c) =>
            {
                if (c.Type == VoiceChatType.NEARBY)
                    c = new VoiceChatNametagComponent(isSpeaking: false, type: VoiceChatType.NEARBY) { IsRemoving = true };
            });

            previousActiveSpeakers.Clear();
        }


        private bool IsNearbyActive() =>
            nearbyStateModel.State.Value is NearbyVoiceChatState.IDLE or NearbyVoiceChatState.SPEAKING;

        private void OnActiveSpeakersUpdated()
        {
            if (!IsNearbyActive()) return;

            string localPlayer = islandRoom.Participants.LocalParticipant().Identity;
            bool localInSpeakingState = nearbyStateModel.State.Value == NearbyVoiceChatState.SPEAKING;

            currentActiveSpeakers.Clear();

            foreach (string? identity in islandRoom.ActiveSpeakers)
                if (!string.IsNullOrEmpty(identity))
                    currentActiveSpeakers.Add(identity);

            // Local: update IsSpeaking only while publishing (State==SPEAKING). Leaving SPEAKING is handled by OnStateChanged.
            if (localInSpeakingState && !string.IsNullOrEmpty(localPlayer))
                world.AddOrSet(playerEntity, new VoiceChatNametagComponent(isSpeaking: currentActiveSpeakers.Contains(localPlayer), type: VoiceChatType.NEARBY));

            // Speakers who stopped speaking: flip IsSpeaking to false (dots). Component stays until TrackUnsubscribed removes it.
            foreach (string identity in previousActiveSpeakers)
            {
                if (currentActiveSpeakers.Contains(identity)) continue;
                if (identity == localPlayer) continue;

                if (entityParticipantTable.TryGet(identity, out IReadOnlyEntityParticipantTable.Entry entry)
                    && world.Has<VoiceChatNametagComponent>(entry.Entity))
                    world.AddOrSet(entry.Entity, new VoiceChatNametagComponent(isSpeaking: false, type: VoiceChatType.NEARBY, isHushed: muteService.IsMuted(identity)));
            }

            // Speakers who started speaking: flip IsSpeaking to true (wave).
            foreach (string identity in currentActiveSpeakers)
            {
                if (identity == localPlayer) continue;

                if (entityParticipantTable.TryGet(identity, out IReadOnlyEntityParticipantTable.Entry entry))
                    world.AddOrSet(entry.Entity, new VoiceChatNametagComponent(isSpeaking: true, type: VoiceChatType.NEARBY, isHushed: muteService.IsMuted(identity)));
            }

            previousActiveSpeakers.Clear();
            previousActiveSpeakers.UnionWith(currentActiveSpeakers);
        }
    }
}
