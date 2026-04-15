using Arch.Core;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Utilities;
using LiveKit.Rooms;
using LiveKit.Rooms.Participants;
using System;
using System.Collections.Generic;
using Utility.Arch;

namespace DCL.VoiceChat
{
    public class VoiceChatNametagsHandler : IDisposable
    {
        private readonly IRoom room;
        private readonly IReadonlyReactiveProperty<VoiceChatActivityState> activityState;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly IDisposable statusSubscription;

        private readonly World world;
        private readonly Entity playerEntity;

        private HashSet<string> activeSpeakers = new ();
        private bool disposed;
        private bool localPlayerSpeaking;

        public VoiceChatNametagsHandler(
            IRoom room,
            IReadonlyReactiveProperty<VoiceChatActivityState> activityState,
            IReadOnlyEntityParticipantTable entityParticipantTable,
            World world,
            Entity playerEntity)
        {
            this.room = room;
            this.activityState = activityState;
            this.entityParticipantTable = entityParticipantTable;
            this.world = world;
            this.playerEntity = playerEntity;

            statusSubscription = activityState.Subscribe(OnActivityStateChanged);
            room.ActiveSpeakers.Updated += OnActiveSpeakersUpdated;
            room.Participants.UpdatesFromParticipant += OnParticipantUpdated;
            entityParticipantTable.OnRegistered += OnEntityRegistered;

            // Subscribe does not fire for the current value, so sync existing remote speakers
            // if voice chat is already active when the handler is created (e.g. user was on loading screen
            // while others were already speaking). Only speaker diff is updated here — local player state
            // cannot be queried yet because LocalParticipant may not be initialized at construction time.
            if (activityState.Value == VoiceChatActivityState.ACTIVE)
                UpdateSpeakersDiff(room.ActiveSpeakers);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            statusSubscription?.Dispose();
            room.Participants.UpdatesFromParticipant -= OnParticipantUpdated;
            room.ActiveSpeakers.Updated -= OnActiveSpeakersUpdated;
            entityParticipantTable.OnRegistered -= OnEntityRegistered;

            (activityState as IDisposable)?.Dispose();

            MarkAllRemoving();
            MarkLocalPlayerRemoving();
        }

        private void OnActivityStateChanged(VoiceChatActivityState state)
        {
            switch (state)
            {
                case VoiceChatActivityState.ACTIVE:
                    OnActiveSpeakersUpdated();
                    break;
                case VoiceChatActivityState.INACTIVE:
                    MarkAllRemoving();
                    MarkLocalPlayerRemoving();
                    break;
            }
        }

        private void OnActiveSpeakersUpdated()
        {
            if (activityState.Value != VoiceChatActivityState.ACTIVE) return;

            UpdateSpeakersDiff(room.ActiveSpeakers);
            UpdateLocalPlayerSpeakingState();
        }

        /// <summary>
        /// Handles the race condition where ActiveSpeakers reported a speaker before their
        /// ECS entity was registered in the participant table. When the entity finally appears,
        /// retry setting the nametag component.
        /// </summary>
        private void OnEntityRegistered(string walletId)
        {
            if (activityState.Value != VoiceChatActivityState.ACTIVE) return;

            if (activeSpeakers.Contains(walletId))
                SetSpeakingState(walletId, true);
        }

        private void OnParticipantUpdated(LKParticipant participant, UpdateFromParticipant update)
        {
            if (update == UpdateFromParticipant.Disconnected)
            {
                if (entityParticipantTable.TryGet(participant.Identity, out IReadOnlyEntityParticipantTable.Entry entry))
                    world.TryRemove<VoiceChatNametagComponent>(entry.Entity);

                activeSpeakers.Remove(participant.Identity);
            }
        }

        private void UpdateSpeakersDiff(IEnumerable<string> currentSpeakers)
        {
            var newActiveSpeakers = new HashSet<string>();

            foreach (string speakerId in currentSpeakers)
            {
                newActiveSpeakers.Add(speakerId);

                if (!activeSpeakers.Contains(speakerId))
                    SetSpeakingState(speakerId, true);
            }

            foreach (string oldSpeakerId in activeSpeakers)
            {
                if (!newActiveSpeakers.Contains(oldSpeakerId))
                    SetSpeakingState(oldSpeakerId, false);
            }

            activeSpeakers = newActiveSpeakers;
        }

        private void MarkAllRemoving()
        {
            foreach (string participantId in activeSpeakers)
                if (entityParticipantTable.TryGet(participantId, out IReadOnlyEntityParticipantTable.Entry entry))
                    world.AddOrSet(entry.Entity, new VoiceChatNametagComponent(false) { IsRemoving = true });

            activeSpeakers.Clear();
        }

        private void SetSpeakingState(string participantId, bool isSpeaking)
        {
            if (entityParticipantTable.TryGet(participantId, out IReadOnlyEntityParticipantTable.Entry entry))
                world.AddOrSet(entry.Entity, new VoiceChatNametagComponent(isSpeaking));
        }

        private void UpdateLocalPlayerSpeakingState()
        {
            string localIdentity = room.Participants.LocalParticipant().Identity;

            var isSpeaking = false;
            foreach (string speakerId in room.ActiveSpeakers)
                if (speakerId == localIdentity)
                {
                    isSpeaking = true;
                    break;
                }

            if (isSpeaking == localPlayerSpeaking)
                return;

            localPlayerSpeaking = isSpeaking;
            world.AddOrSet(playerEntity, new VoiceChatNametagComponent(isSpeaking));
        }

        private void MarkLocalPlayerRemoving()
        {
            localPlayerSpeaking = false;
            world.AddOrSet(playerEntity, new VoiceChatNametagComponent(false) { IsRemoving = true });
        }
    }
}
