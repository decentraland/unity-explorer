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
    /// Suppresses nametag indicators while a Private/Community call is active.
    /// </summary>
    public class ProximityNametagsHandler : IDisposable
    {
        private readonly IRoom islandRoom;
        private readonly World world;
        private readonly Entity playerEntity;
        private readonly string localIdentity;
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
            string localIdentity)
        {
            this.islandRoom = islandRoom;
            this.world = world;
            this.playerEntity = playerEntity;
            this.localIdentity = localIdentity;

            tracker = new ActiveSpeakersDiffTracker(entityParticipantTable, world);

            islandRoom.ActiveSpeakers.Updated += OnActiveSpeakersUpdated;
            islandRoom.Participants.UpdatesFromParticipant += OnParticipantUpdated;
            callStatusSubscription = callStatus.Subscribe(OnCallStatusChanged);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            callStatusSubscription.Dispose();
            islandRoom.ActiveSpeakers.Updated -= OnActiveSpeakersUpdated;
            islandRoom.Participants.UpdatesFromParticipant -= OnParticipantUpdated;

            tracker.MarkAllRemoving();
            MarkLocalPlayerRemoving();
        }

        private void OnActiveSpeakersUpdated()
        {
            if (suppressed) return;

            tracker.Update(islandRoom.ActiveSpeakers);
            UpdateLocalPlayerSpeakingState(IsIdentityAmongActiveSpeakers(localIdentity));
        }

        private void UpdateLocalPlayerSpeakingState(bool isSpeaking)
        {
            if (isSpeaking != localPlayerSpeaking)
            {
                localPlayerSpeaking = isSpeaking;
                world.AddOrSet(playerEntity, new VoiceChatNametagComponent(isSpeaking));
            }
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
                UpdateLocalPlayerSpeakingState(IsIdentityAmongActiveSpeakers(localIdentity));
            }
        }
    }
}
