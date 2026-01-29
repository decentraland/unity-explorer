using LiveKit.Rooms.Participants;
using System;
using System.Collections.Generic;

namespace DCL.Multiplayer.Connections.Rooms.Nulls
{
    public class NullParticipantsHub : IParticipantsHub
    {
        private static readonly IReadOnlyDictionary<string, LKParticipant> EMPTY_DICTIONARY = new Dictionary<string, LKParticipant>();

        public static readonly NullParticipantsHub INSTANCE = new ();
        private static readonly LKParticipant NULL_PARTICIPANT = new ();

        public event ParticipantDelegate? UpdatesFromParticipant;

        public LKParticipant LocalParticipant() =>
            NULL_PARTICIPANT;

        public LKParticipant? RemoteParticipant(string identity) =>
            null;

        public IReadOnlyDictionary<string, LKParticipant> RemoteParticipantIdentities() =>
            EMPTY_DICTIONARY;
    }
}
