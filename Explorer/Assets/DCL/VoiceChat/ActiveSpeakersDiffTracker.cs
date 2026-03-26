using Arch.Core;
using DCL.Multiplayer.Profiles.Tables;
using System.Collections.Generic;
using Utility.Arch;

namespace DCL.VoiceChat
{
    /// <summary>
    /// Tracks active speaker changes by diffing consecutive snapshots from <see cref="LiveKit.Rooms.ActiveSpeakers.IActiveSpeakers"/>
    /// and writes <see cref="VoiceChatNametagComponent"/> to ECS entities via <see cref="IReadOnlyEntityParticipantTable"/>.
    /// Shared by both Community and Proximity nametag handlers to avoid duplicating diff + entity-write logic.
    /// </summary>
    internal class ActiveSpeakersDiffTracker
    {
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly World world;

        private HashSet<string> activeSpeakers = new ();
        private readonly HashSet<string> touchedParticipants = new ();

        internal ActiveSpeakersDiffTracker(
            IReadOnlyEntityParticipantTable entityParticipantTable,
            World world)
        {
            this.entityParticipantTable = entityParticipantTable;
            this.world = world;
        }

        internal void Update(IEnumerable<string> currentSpeakers)
        {
            var newActiveSpeakers = new HashSet<string>();

            foreach (string speakerId in currentSpeakers)
            {
                newActiveSpeakers.Add(speakerId);

                if (!activeSpeakers.Contains(speakerId) || !touchedParticipants.Contains(speakerId))
                    SetSpeakingState(speakerId, true);
            }

            foreach (string oldSpeakerId in activeSpeakers)
            {
                if (!newActiveSpeakers.Contains(oldSpeakerId))
                    SetSpeakingState(oldSpeakerId, false);
            }

            activeSpeakers = newActiveSpeakers;
        }

        internal void MarkAllRemoving()
        {
            foreach (string participantId in touchedParticipants)
            {
                if (entityParticipantTable.TryGet(participantId, out IReadOnlyEntityParticipantTable.Entry entry))
                    world.AddOrSet(entry.Entity, new VoiceChatNametagComponent(false) { IsRemoving = true });
            }

            touchedParticipants.Clear();
            activeSpeakers.Clear();
        }

        internal void RemoveParticipant(string participantId)
        {
            if (entityParticipantTable.TryGet(participantId, out IReadOnlyEntityParticipantTable.Entry entry))
                world.TryRemove<VoiceChatNametagComponent>(entry.Entity);

            activeSpeakers.Remove(participantId);
            touchedParticipants.Remove(participantId);
        }

        private void SetSpeakingState(string participantId, bool isSpeaking)
        {
            if (entityParticipantTable.TryGet(participantId, out IReadOnlyEntityParticipantTable.Entry entry))
            {
                world.AddOrSet(entry.Entity, new VoiceChatNametagComponent(isSpeaking));
                touchedParticipants.Add(participantId);
            }
        }
    }
}
