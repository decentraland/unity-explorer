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

        private void OnUpdatesFromParticipant(Participant participant, UpdateFromParticipant update) =>
            UpdatesFromParticipant?.Invoke(participant, update);

        public Participant LocalParticipant() =>
            assigned.EnsureAssigned().LocalParticipant();

        public Participant? RemoteParticipant(string identity) =>
            assigned.EnsureAssigned().RemoteParticipant(identity);

        public IReadOnlyCollection<string> RemoteParticipantIdentities() =>
            assigned.EnsureAssigned().RemoteParticipantIdentities();
    }
}
