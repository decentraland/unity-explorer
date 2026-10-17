using DCL.Multiplayer.Connections.Rooms.Nulls;
using LiveKit.Rooms.Participants;
using System.Collections.Generic;

namespace DCL.Multiplayer.Connections.Rooms.Interior
{
    public class InteriorParticipantsHub : IParticipantsHub, IInterior<IParticipantsHub>
    {
        private IParticipantsHub assigned = NullParticipantsHub.INSTANCE;

        public event ParticipantDelegate? UpdatesFromParticipant;

        public void Assign(IParticipantsHub value, out IParticipantsHub? previous)
        {
            previous = assigned;
            previous.UpdatesFromParticipant -= OnUpdatesFromParticipant;

            assigned = value;
            value.UpdatesFromParticipant += OnUpdatesFromParticipant;

            previous = previous is NullParticipantsHub ? null : previous;
        }

        private void OnUpdatesFromParticipant(LKParticipant participant, UpdateFromParticipant update) =>
            UpdatesFromParticipant?.Invoke(participant, update);

        public LKParticipant LocalParticipant() =>
            assigned.EnsureAssigned().LocalParticipant();

        public LKParticipant? RemoteParticipant(string identity) =>
            assigned.EnsureAssigned().RemoteParticipant(identity);

        public IReadOnlyDictionary<string, LKParticipant> RemoteParticipantIdentities() =>
            assigned.EnsureAssigned().RemoteParticipantIdentities();
    }
}
