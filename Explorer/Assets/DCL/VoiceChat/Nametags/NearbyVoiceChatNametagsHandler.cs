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

        private readonly HashSet<string> activeSpeakers = new ();
        private readonly HashSet<string> newActiveSpeakers = new ();

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
                    world.AddOrSet(playerEntity, new VoiceChatNametagComponent(isSpeaking: false, type: VoiceChatType.NEARBY) { IsRemoving = true });
                    break;

                case NearbyVoiceChatState.OPEN_MIC:
                    string localPlayer = islandRoom.Participants.LocalParticipant().Identity;
                    bool isSpeaking = !string.IsNullOrEmpty(localPlayer) && islandRoom.ActiveSpeakers.Contains(localPlayer);
                    world.AddOrSet(playerEntity, new VoiceChatNametagComponent(isSpeaking: isSpeaking, type: VoiceChatType.NEARBY));
                    break;
            }
        }

        private void OnActiveSpeakersUpdated()
        {
            if (!IsNearbyActive()) return;

            string localPlayer = islandRoom.Participants.LocalParticipant().Identity;

            // Handle local separately (never stored in the sets).
            if (nearbyStateModel.State.Value == NearbyVoiceChatState.OPEN_MIC)
            {
                bool localSpeaking = !string.IsNullOrEmpty(localPlayer) && islandRoom.ActiveSpeakers.Contains(localPlayer);
                world.AddOrSet(playerEntity, new VoiceChatNametagComponent(localSpeaking, VoiceChatType.NEARBY));
            }

            // Populate and start nametag sound wave anim for new speakers.
            // activeSpeakers.Remove returns true when speaker was in previous set → continuing speaker, skip re-write
            // After this loop activeSpeakers contains only those who stopped speaking.
            newActiveSpeakers.Clear();
            foreach (string? identity in islandRoom.ActiveSpeakers)
            {
                if (string.IsNullOrEmpty(identity) || identity == localPlayer) continue;

                newActiveSpeakers.Add(identity);

                if (!activeSpeakers.Remove(identity) && entityParticipantTable.TryGet(identity, out IReadOnlyEntityParticipantTable.Entry entry))
                    world.AddOrSet(entry.Entity, new VoiceChatNametagComponent(isSpeaking: true, type: VoiceChatType.NEARBY, isHushed: muteService.IsMuted(identity)));
            }

            // Speakers who stopped: flip IsSpeaking to false (green dots).
            foreach (string identity in activeSpeakers)
            {
                if (entityParticipantTable.TryGet(identity, out IReadOnlyEntityParticipantTable.Entry entry) && world.Has<VoiceChatNametagComponent>(entry.Entity))
                    world.AddOrSet(entry.Entity, new VoiceChatNametagComponent(isSpeaking: false, type: VoiceChatType.NEARBY, isHushed: muteService.IsMuted(identity)));
            }

            activeSpeakers.Clear();
            activeSpeakers.UnionWith(newActiveSpeakers);
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

            activeSpeakers.Clear();
        }

        private bool IsNearbyActive() =>
            nearbyStateModel.State.Value is NearbyVoiceChatState.IDLE or NearbyVoiceChatState.OPEN_MIC;
    }
}
