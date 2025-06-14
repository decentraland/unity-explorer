using Arch.Core;
using DCL.Multiplayer.Profiles.Tables;
using LiveKit.Rooms;
using LiveKit.Rooms.Participants;
using System;
using System.Collections.Generic;
using Utility.Arch;
using DCL.Diagnostics;

namespace DCL.VoiceChat
{
    public class VoiceChatNametagsHandler : IDisposable
    {
        private readonly IRoom voiceChatRoom;
        private readonly IVoiceChatCallStatusService voiceChatCallStatusService;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly World world;
        private readonly Entity playerEntity;
        private HashSet<string> activeSpeakers = new();

        private bool disposed;

        public VoiceChatNametagsHandler(
            IRoom voiceChatRoom,
            IVoiceChatCallStatusService voiceChatCallStatusService,
            IReadOnlyEntityParticipantTable entityParticipantTable,
            World world,
            Entity playerEntity)
        {
            this.voiceChatRoom = voiceChatRoom;
            this.voiceChatCallStatusService = voiceChatCallStatusService;
            this.entityParticipantTable = entityParticipantTable;
            this.world = world;
            this.playerEntity = playerEntity;

            voiceChatCallStatusService.StatusChanged += OnCallStatusChanged;
            voiceChatRoom.Participants.UpdatesFromParticipant += OnParticipantUpdated;
            voiceChatRoom.ActiveSpeakers.Updated += OnActiveSpeakersUpdated;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            voiceChatCallStatusService.StatusChanged -= OnCallStatusChanged;
            voiceChatRoom.Participants.UpdatesFromParticipant -= OnParticipantUpdated;
            voiceChatRoom.ActiveSpeakers.Updated -= OnActiveSpeakersUpdated;
        }

        private void OnActiveSpeakersUpdated()
        {
            var newActiveSpeakers = new HashSet<string>();

            foreach (string speakerId in voiceChatRoom.ActiveSpeakers)
            {
                newActiveSpeakers.Add(speakerId);
                if (!activeSpeakers.Contains(speakerId))
                {
                    SetIsSpeaking(speakerId, true);
                }
            }

            foreach (string oldSpeakerId in activeSpeakers)
            {
                if (!newActiveSpeakers.Contains(oldSpeakerId))
                {
                    SetIsSpeaking(oldSpeakerId, false);
                }
            }

            activeSpeakers = newActiveSpeakers;
        }

        private void SetIsSpeaking(string participantId, bool isSpeaking)
        {
            if (entityParticipantTable.TryGet(participantId, out var entry))
            {
                world.AddOrSet(entry.Entity, new VoiceChatNametagComponent(isSpeaking));
                ReportHub.LogError(ReportCategory.VOICE_CHAT, $"Participant {participantId} {(isSpeaking ? "started" : "stopped")} speaking");
            }
            else
            {
                world.AddOrSet(playerEntity, new VoiceChatNametagComponent(isSpeaking));
            }
        }

        private void OnParticipantUpdated(Participant participant, UpdateFromParticipant update)
        {
            switch (update)
            {
                case UpdateFromParticipant.Disconnected:
                    if (entityParticipantTable.TryGet(participant.Identity, out var entry))
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
                    world.AddOrSet(playerEntity, new VoiceChatNametagComponent(false));
                    OnActiveSpeakersUpdated();
                    break;

                case VoiceChatStatus.VOICE_CHAT_ENDING_CALL:
                case VoiceChatStatus.DISCONNECTED:
                case VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR:
                    world.AddOrSet(playerEntity, new VoiceChatNametagComponent(false) { IsRemoving = true });
                    activeSpeakers.Clear();

                    foreach (string participantId in voiceChatRoom.Participants.RemoteParticipantIdentities())
                    {
                        if (entityParticipantTable.TryGet(participantId, out var entry))
                        {
                            world.AddOrSet(entry.Entity, new VoiceChatNametagComponent(false) { IsRemoving = true });
                        }
                    }
                    break;
            }
        }
    }
}
