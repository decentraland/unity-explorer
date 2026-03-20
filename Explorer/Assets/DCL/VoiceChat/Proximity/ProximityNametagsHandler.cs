using Arch.Core;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Utilities;
using LiveKit.Rooms;
using LiveKit.Rooms.Participants;
using System;
using Utility.Arch;

namespace DCL.VoiceChat
{
    /// <summary>
    /// Bridges Island Room active-speaker events to <see cref="VoiceChatNametagComponent"/> on avatar entities.
    /// Handles both remote players (via <see cref="ActiveSpeakersDiffTracker"/>) and the local player directly.
    /// Sets <see cref="VoiceChatNametagComponent.IsHushed"/> for muted speakers so the nametag shows the hushed icon
    /// only while they are speaking.
    /// Suppresses nametag indicators while a Private/Community call is active.
    /// </summary>
    public class ProximityNametagsHandler : IDisposable
    {
        private readonly IRoom islandRoom;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly World world;
        private readonly Entity playerEntity;
        private readonly string localIdentity;
        private readonly ProximityMuteService muteService;
        private readonly ActiveSpeakersDiffTracker tracker;
        private readonly IDisposable callStatusSubscription;

        private bool disposed;
        private bool suppressed;
        private bool localPlayerSpeaking;

        public ProximityNametagsHandler(
            IRoom islandRoom,
            IReadOnlyEntityParticipantTable entityParticipantTable,
            World world,
            IReadonlyReactiveProperty<VoiceChatStatus> callStatus,
            Entity playerEntity,
            string localIdentity,
            ProximityMuteService muteService)
        {
            this.islandRoom = islandRoom;
            this.entityParticipantTable = entityParticipantTable;
            this.world = world;
            this.playerEntity = playerEntity;
            this.localIdentity = localIdentity;
            this.muteService = muteService;

            tracker = new ActiveSpeakersDiffTracker(entityParticipantTable, world);

            islandRoom.ActiveSpeakers.Updated += OnActiveSpeakersUpdated;
            islandRoom.Participants.UpdatesFromParticipant += OnParticipantUpdated;
            callStatusSubscription = callStatus.Subscribe(OnCallStatusChanged);
            muteService.MuteStateChanged += OnMuteStateChanged;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            callStatusSubscription.Dispose();
            islandRoom.ActiveSpeakers.Updated -= OnActiveSpeakersUpdated;
            islandRoom.Participants.UpdatesFromParticipant -= OnParticipantUpdated;
            muteService.MuteStateChanged -= OnMuteStateChanged;

            tracker.MarkAllRemoving();
            MarkLocalPlayerRemoving();
        }

        private void OnActiveSpeakersUpdated()
        {
            if (suppressed) return;

            tracker.Update(islandRoom.ActiveSpeakers);
            ApplyHushedStateToActiveSpeakers();
            UpdateLocalPlayerSpeakingState();
        }

        private void ApplyHushedStateToActiveSpeakers()
        {
            foreach (string speakerId in islandRoom.ActiveSpeakers)
            {
                if (!muteService.IsMuted(speakerId)) continue;
                if (!entityParticipantTable.TryGet(speakerId, out IReadOnlyEntityParticipantTable.Entry entry)) continue;

                world.AddOrSet(entry.Entity, new VoiceChatNametagComponent(true, isHushed: true));
            }
        }

        private void UpdateLocalPlayerSpeakingState()
        {
            bool isSpeaking = IsIdentityAmongActiveSpeakers(localIdentity);

            if (isSpeaking == localPlayerSpeaking)
                return;

            localPlayerSpeaking = isSpeaking;
            world.AddOrSet(playerEntity, new VoiceChatNametagComponent(isSpeaking));
        }

        private void OnMuteStateChanged(string walletId, bool isMuted)
        {
            if (suppressed) return;
            if (!IsIdentityAmongActiveSpeakers(walletId)) return;

            if (entityParticipantTable.TryGet(walletId, out IReadOnlyEntityParticipantTable.Entry entry))
                world.AddOrSet(entry.Entity, new VoiceChatNametagComponent(true, isHushed: isMuted));
        }

        private bool IsIdentityAmongActiveSpeakers(string identity)
        {
            foreach (string speakerId in islandRoom.ActiveSpeakers)
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

        private void OnParticipantUpdated(Participant participant, UpdateFromParticipant update)
        {
            if (update == UpdateFromParticipant.Disconnected)
                tracker.RemoveParticipant(participant.Identity);
        }

        private void OnCallStatusChanged(VoiceChatStatus status)
        {
            if (status == VoiceChatStatus.VOICE_CHAT_IN_CALL)
            {
                suppressed = true;
                tracker.MarkAllRemoving();
                MarkLocalPlayerRemoving();
            }
            else if (status.IsNotConnected())
            {
                suppressed = false;
                tracker.Update(islandRoom.ActiveSpeakers);
                ApplyHushedStateToActiveSpeakers();
                UpdateLocalPlayerSpeakingState();
            }
        }
    }
}
