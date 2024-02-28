using LiveKit.Rooms.Participants;
using System.Collections.Generic;

namespace DCL.Multiplayer.Connections.Rooms.Interior
{
    public class InteriorParticipantsHub : IParticipantsHub, IInterior<IParticipantsHub>
    {
        private IParticipantsHub? assigned;

        public event ParticipantDelegate? UpdatesFromParticipant;

        public void Assign(IParticipantsHub value, out IParticipantsHub? previous)
        {
            previous = assigned;

            if (previous != null)
                previous.UpdatesFromParticipant -= OnUpdatesFromParticipant;

            assigned = value;
            value.UpdatesFromParticipant += OnUpdatesFromParticipant;
        }

        private void OnUpdatesFromParticipant(Participant participant, UpdateFromParticipant update) =>
            UpdatesFromParticipant?.Invoke(participant, update);

        public Participant LocalParticipant() =>
            assigned.EnsureAssigned().LocalParticipant();

        public Participant? RemoteParticipant(string sid) =>
            assigned.EnsureAssigned().RemoteParticipant(sid);

        public IReadOnlyCollection<string> RemoteParticipantSids() =>
            assigned.EnsureAssigned().RemoteParticipantSids();
    }
}
