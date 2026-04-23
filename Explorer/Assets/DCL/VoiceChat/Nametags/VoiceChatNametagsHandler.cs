using Arch.Core;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Utilities;
using DCL.LiveKit.Public;
using LiveKit.Rooms;
using LiveKit.Rooms.Participants;
using System;
using System.Collections.Generic;
using Utility.Arch;

namespace DCL.VoiceChat
{
    public class VoiceChatNametagsHandler : IDisposable
    {
        private readonly IRoom voiceChatRoom;
        private readonly IVoiceChatOrchestratorState voiceChatOrchestratorState;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly World world;
        private readonly Entity playerEntity;
        private readonly IDisposable statusSubscription;

        private readonly HashSet<string> activeSpeakers = new ();
        private readonly HashSet<string> newActiveSpeakers = new ();
        private bool disposed;

        private VoiceChatType currentType => voiceChatOrchestratorState.CurrentVoiceChatType.Value;

        public VoiceChatNametagsHandler(
            IRoom voiceChatRoom,
            IVoiceChatOrchestratorState voiceChatOrchestratorState,
            IReadOnlyEntityParticipantTable entityParticipantTable,
            World world,
            Entity playerEntity)
        {
            this.voiceChatRoom = voiceChatRoom;
            this.voiceChatOrchestratorState = voiceChatOrchestratorState;
            this.entityParticipantTable = entityParticipantTable;
            this.world = world;
            this.playerEntity = playerEntity;

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
            // After this loop activeSpeakers contains only those who stopped speaking.
            newActiveSpeakers.Clear();
            foreach (string speakerId in voiceChatRoom.ActiveSpeakers)
            {
                newActiveSpeakers.Add(speakerId);

                if (!activeSpeakers.Remove(speakerId))
                    SetIsSpeaking(speakerId, true);
            }

            foreach (string oldSpeakerId in activeSpeakers)
                SetIsSpeaking(oldSpeakerId, false);

            activeSpeakers.Clear();
            activeSpeakers.UnionWith(newActiveSpeakers);
        }

        private void SetIsSpeaking(string participantId, bool isSpeaking)
        {
            if (entityParticipantTable.TryGet(participantId, out IReadOnlyEntityParticipantTable.Entry entry))
                world.AddOrSet(entry.Entity, new VoiceChatNametagComponent(isSpeaking, currentType));
            else if (voiceChatRoom.Participants.LocalParticipant()?.Identity == participantId)
                world.AddOrSet(playerEntity, new VoiceChatNametagComponent(isSpeaking, currentType));
        }

        private void OnParticipantUpdated(LKParticipant participant, UpdateFromParticipant update)
        {
            switch (update)
            {
                case UpdateFromParticipant.Disconnected:
                    if (entityParticipantTable.TryGet(participant.Identity, out IReadOnlyEntityParticipantTable.Entry entry))
                    {
                        world.TryRemove<VoiceChatNametagComponent>(entry.Entity);
                        activeSpeakers.Remove(participant.Identity);
                    }

                    break;
            }
        }

        private void OnCallStatusChanged(VoiceChatStatus status)
        {
            switch (status)
            {
                case VoiceChatStatus.VOICE_CHAT_IN_CALL:
                    world.AddOrSet(playerEntity, new VoiceChatNametagComponent(false, currentType));
                    OnActiveSpeakersUpdated();
                    break;

                case VoiceChatStatus.VOICE_CHAT_ENDING_CALL:
                case VoiceChatStatus.DISCONNECTED:
                case VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR:
                    world.AddOrSet(playerEntity, new VoiceChatNametagComponent(false, currentType) { IsRemoving = true });
                    activeSpeakers.Clear();

                    foreach ((string participantId, _) in voiceChatRoom.Participants.RemoteParticipantIdentities())
                    {
                        if (entityParticipantTable.TryGet(participantId, out IReadOnlyEntityParticipantTable.Entry entry))
                            world.AddOrSet(entry.Entity, new VoiceChatNametagComponent(false, currentType) { IsRemoving = true });
                    }

                    break;
            }
        }
    }
}
