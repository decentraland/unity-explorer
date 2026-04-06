using Arch.Core;
using DCL.AvatarRendering.AvatarShape;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Utilities;
using LiveKit.Rooms;
using LiveKit.Rooms.Participants;
using System;
using System.Collections.Generic;

namespace DCL.VoiceChat
{
    /// <summary>
    ///     Listens to voice-chat speaking events from the LiveKit room and enqueues
    ///     speaking-state changes into <see cref="AvatarMouthInputQueue"/> so that
    ///     <c>AvatarFacialExpressionSystem</c> can apply them to ECS on the next frame.
    ///     Entity manipulation must happen inside ECS systems; this handler only buffers.
    ///     <para>
    ///         LiveKit callbacks (<c>ActiveSpeakers.Updated</c>, <c>UpdatesFromParticipant</c>,
    ///         and the reactive-property observer for <c>CurrentCallStatus</c>) are not guaranteed
    ///         to fire on the Unity main thread. All methods that touch <see cref="activeSpeakers"/>
    ///         or <see cref="nextActiveSpeakers"/> must hold <see cref="speakerLock"/>.
    ///     </para>
    /// </summary>
    public class VoiceChatMouthAnimationHandler : IDisposable
    {
        private readonly IRoom voiceChatRoom;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly Entity playerEntity;
        private readonly AvatarMouthInputQueue mouthInputQueue;
        private readonly IDisposable statusSubscription;

        // Guards activeSpeakers and nextActiveSpeakers against concurrent callback access.
        private readonly object speakerLock = new();

        // Reused across calls to avoid per-event allocation.
        private readonly HashSet<string> activeSpeakers = new();
        private readonly HashSet<string> nextActiveSpeakers = new();

        private bool disposed;

        public VoiceChatMouthAnimationHandler(
            IRoom voiceChatRoom,
            IVoiceChatOrchestratorState voiceChatOrchestratorState,
            IReadOnlyEntityParticipantTable entityParticipantTable,
            AvatarMouthInputQueue mouthInputQueue,
            Entity playerEntity)
        {
            this.voiceChatRoom = voiceChatRoom;
            this.entityParticipantTable = entityParticipantTable;
            this.mouthInputQueue = mouthInputQueue;
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
            lock (speakerLock)
            {
                // Build the new active-speaker set without allocating a new HashSet.
                nextActiveSpeakers.Clear();

                foreach (string speakerId in voiceChatRoom.ActiveSpeakers)
                    nextActiveSpeakers.Add(speakerId);

                // Newly speaking: in next but not in current.
                foreach (string speakerId in nextActiveSpeakers)
                    if (!activeSpeakers.Contains(speakerId))
                        EnqueueSpeaking(speakerId, true);

                // Stopped speaking: in current but not in next.
                foreach (string speakerId in activeSpeakers)
                    if (!nextActiveSpeakers.Contains(speakerId))
                        EnqueueSpeaking(speakerId, false);

                // Replace active set in-place.
                activeSpeakers.Clear();
                activeSpeakers.UnionWith(nextActiveSpeakers);
            }
        }

        private void EnqueueSpeaking(string participantId, bool isSpeaking)
        {
            if (entityParticipantTable.TryGet(participantId, out IReadOnlyEntityParticipantTable.Entry entry))
                mouthInputQueue.EnqueueSpeaking(entry.Entity, isSpeaking);
            else if (voiceChatRoom.Participants.LocalParticipant().Identity == participantId)
                mouthInputQueue.EnqueueSpeaking(playerEntity, isSpeaking);
        }

        private void OnParticipantUpdated(Participant participant, UpdateFromParticipant update)
        {
            if (update != UpdateFromParticipant.Disconnected) return;

            lock (speakerLock)
            {
                if (entityParticipantTable.TryGet(participant.Identity, out IReadOnlyEntityParticipantTable.Entry entry))
                {
                    mouthInputQueue.EnqueueSpeaking(entry.Entity, false);
                    activeSpeakers.Remove(participant.Identity);
                }
            }
        }

        private void OnCallStatusChanged(VoiceChatStatus status)
        {
            switch (status)
            {
                case VoiceChatStatus.VOICE_CHAT_IN_CALL:
                    mouthInputQueue.EnqueueSpeaking(playerEntity, false);
                    OnActiveSpeakersUpdated();
                    break;

                case VoiceChatStatus.VOICE_CHAT_ENDING_CALL:
                case VoiceChatStatus.DISCONNECTED:
                case VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR:
                    mouthInputQueue.EnqueueSpeaking(playerEntity, false);

                    lock (speakerLock)
                    {
                        activeSpeakers.Clear();

                        foreach ((string participantId, _) in voiceChatRoom.Participants.RemoteParticipantIdentities())
                        {
                            if (entityParticipantTable.TryGet(participantId, out IReadOnlyEntityParticipantTable.Entry entry))
                                mouthInputQueue.EnqueueSpeaking(entry.Entity, false);
                        }
                    }

                    break;
            }
        }
    }
}
