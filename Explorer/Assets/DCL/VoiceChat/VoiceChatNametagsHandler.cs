using Arch.Core;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Utilities;
using LiveKit.Rooms;
using LiveKit.Rooms.Participants;
using System;
using Utility.Arch;

namespace DCL.VoiceChat
{
    public class VoiceChatNametagsHandler : IDisposable
    {
        private readonly IRoom voiceChatRoom;
        private readonly World world;
        private readonly Entity playerEntity;
        private readonly ActiveSpeakersDiffTracker tracker;
        private readonly IDisposable statusSubscription;

        private bool disposed;

        public VoiceChatNametagsHandler(
            IRoom voiceChatRoom,
            IVoiceChatOrchestratorState voiceChatOrchestratorState,
            IReadOnlyEntityParticipantTable entityParticipantTable,
            World world,
            Entity playerEntity)
        {
            this.voiceChatRoom = voiceChatRoom;
            this.world = world;
            this.playerEntity = playerEntity;

            tracker = new ActiveSpeakersDiffTracker(entityParticipantTable, world);

            statusSubscription = voiceChatOrchestratorState.CurrentCallStatus.Subscribe(OnCallStatusChanged);
            voiceChatRoom.Participants.UpdatesFromParticipant += OnParticipantUpdated;
            voiceChatRoom.ActiveSpeakers.Updated += OnActiveSpeakersUpdated;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            statusSubscription?.Dispose();
            voiceChatRoom.Participants.UpdatesFromParticipant -= OnParticipantUpdated;
            voiceChatRoom.ActiveSpeakers.Updated -= OnActiveSpeakersUpdated;
        }

        private void OnActiveSpeakersUpdated()
        {
            tracker.Update(voiceChatRoom.ActiveSpeakers);
        }

        private void OnParticipantUpdated(Participant participant, UpdateFromParticipant update)
        {
            if (update == UpdateFromParticipant.Disconnected)
                tracker.RemoveParticipant(participant.Identity);
        }

        private void OnCallStatusChanged(VoiceChatStatus status)
        {
            switch (status)
            {
                case VoiceChatStatus.VOICE_CHAT_IN_CALL:
                    world.AddOrSet(playerEntity, new VoiceChatNametagComponent(false));
                    tracker.Update(voiceChatRoom.ActiveSpeakers);
                    break;

                case VoiceChatStatus.VOICE_CHAT_ENDING_CALL:
                case VoiceChatStatus.DISCONNECTED:
                case VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR:
                    world.AddOrSet(playerEntity, new VoiceChatNametagComponent(false) { IsRemoving = true });
                    tracker.MarkAllRemoving();
                    break;
            }
        }
    }
}
