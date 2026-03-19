using Arch.Core;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Utilities;
using LiveKit.Rooms;
using LiveKit.Rooms.Participants;
using System;

namespace DCL.VoiceChat
{
    /// <summary>
    /// Bridges Island Room active-speaker events to <see cref="VoiceChatNametagComponent"/> on avatar entities.
    /// Suppresses nametag indicators while a Private/Community call is active.
    /// </summary>
    public class ProximityNametagsHandler : IDisposable
    {
        private readonly IRoom islandRoom;
        private readonly ActiveSpeakersDiffTracker tracker;
        private readonly IDisposable callStatusSubscription;

        private bool disposed;
        private bool suppressed;

        public ProximityNametagsHandler(
            IRoom islandRoom,
            IReadOnlyEntityParticipantTable entityParticipantTable,
            World world,
            IReadonlyReactiveProperty<VoiceChatStatus> callStatus)
        {
            this.islandRoom = islandRoom;

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
        }

        private void OnActiveSpeakersUpdated()
        {
            if (!suppressed)
                tracker.Update(islandRoom.ActiveSpeakers);
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
            }
            else if (status.IsNotConnected())
            {
                suppressed = false;
                tracker.Update(islandRoom.ActiveSpeakers);
            }
        }
    }
}
