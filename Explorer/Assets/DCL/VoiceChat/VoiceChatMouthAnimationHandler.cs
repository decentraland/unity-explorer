using Arch.Core;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Utilities;
using LiveKit.Rooms;
using LiveKit.Rooms.Participants;
using System;
using System.Collections.Generic;
using Utility.Arch;

namespace DCL.VoiceChat
{
    /// <summary>
    ///     Listens to voice-chat speaking events from the LiveKit room and sets
    ///     <see cref="AvatarVoiceChatMouthComponent"/> on the corresponding ECS entities so that
    ///     <c>AvatarFacialAnimationSystem</c> can drive the mouth animation independently of
    ///     the nametag system.
    /// </summary>
    public class VoiceChatMouthAnimationHandler : IDisposable
    {
        private readonly IRoom voiceChatRoom;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly World world;
        private readonly Entity playerEntity;
        private readonly IDisposable statusSubscription;

        private HashSet<string> activeSpeakers = new ();
        private bool disposed;

        public VoiceChatMouthAnimationHandler(
            IRoom voiceChatRoom,
            IVoiceChatOrchestratorState voiceChatOrchestratorState,
            IReadOnlyEntityParticipantTable entityParticipantTable,
            World world,
            Entity playerEntity)
        {
            this.voiceChatRoom = voiceChatRoom;
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
            var newActiveSpeakers = new HashSet<string>();

            foreach (string speakerId in voiceChatRoom.ActiveSpeakers)
            {
                newActiveSpeakers.Add(speakerId);

                if (!activeSpeakers.Contains(speakerId))
                    SetIsSpeaking(speakerId, true);
            }

            foreach (string oldSpeakerId in activeSpeakers)
            {
                if (!newActiveSpeakers.Contains(oldSpeakerId))
                    SetIsSpeaking(oldSpeakerId, false);
            }

            activeSpeakers = newActiveSpeakers;
        }

        private void SetIsSpeaking(string participantId, bool isSpeaking)
        {
            if (entityParticipantTable.TryGet(participantId, out IReadOnlyEntityParticipantTable.Entry entry))
                world.AddOrSet(entry.Entity, new AvatarVoiceChatMouthComponent { IsSpeaking = isSpeaking });
            else if (voiceChatRoom.Participants.LocalParticipant().Identity == participantId)
                world.AddOrSet(playerEntity, new AvatarVoiceChatMouthComponent { IsSpeaking = isSpeaking });
        }

        private void OnParticipantUpdated(Participant participant, UpdateFromParticipant update)
        {
            if (update != UpdateFromParticipant.Disconnected) return;

            if (entityParticipantTable.TryGet(participant.Identity, out IReadOnlyEntityParticipantTable.Entry entry))
            {
                world.AddOrSet(entry.Entity, new AvatarVoiceChatMouthComponent { IsSpeaking = false });
                activeSpeakers.Remove(participant.Identity);
            }
        }

        private void OnCallStatusChanged(VoiceChatStatus status)
        {
            switch (status)
            {
                case VoiceChatStatus.VOICE_CHAT_IN_CALL:
                    world.AddOrSet(playerEntity, new AvatarVoiceChatMouthComponent { IsSpeaking = false });
                    OnActiveSpeakersUpdated();
                    break;

                case VoiceChatStatus.VOICE_CHAT_ENDING_CALL:
                case VoiceChatStatus.DISCONNECTED:
                case VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR:
                    world.AddOrSet(playerEntity, new AvatarVoiceChatMouthComponent { IsSpeaking = false });
                    activeSpeakers.Clear();

                    foreach ((string participantId, _) in voiceChatRoom.Participants.RemoteParticipantIdentities())
                    {
                        if (entityParticipantTable.TryGet(participantId, out IReadOnlyEntityParticipantTable.Entry entry))
                            world.AddOrSet(entry.Entity, new AvatarVoiceChatMouthComponent { IsSpeaking = false });
                    }

                    break;
            }
        }
    }
}
