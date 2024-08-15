using LiveKit.Rooms.Participants;
using System;
using System.Collections.Generic;

namespace DCL.Multiplayer.Connections.Rooms.Nulls
{
    public class NullParticipantsHub : IParticipantsHub
    {
        public static readonly NullParticipantsHub INSTANCE = new ();
        private static readonly Participant NULL_PARTICIPANT = new ();

        public event ParticipantDelegate? UpdatesFromParticipant;

        public Participant LocalParticipant() =>
            NULL_PARTICIPANT;

        public Participant? RemoteParticipant(string identity) =>
            null;

        public IReadOnlyCollection<string> RemoteParticipantIdentities() =>
            ArraySegment<string>.Empty;
    }
}
