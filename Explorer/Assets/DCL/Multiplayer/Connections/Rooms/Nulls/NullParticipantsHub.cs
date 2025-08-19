using LiveKit.Rooms.Participants;
using System;
using System.Collections.Generic;

namespace DCL.Multiplayer.Connections.Rooms.Nulls
{
    public class NullParticipantsHub : IParticipantsHub
    {
        private static readonly IReadOnlyDictionary<string, Participant> EMPTY_DICTIONARY = new Dictionary<string, Participant>();

        public static readonly NullParticipantsHub INSTANCE = new ();
        private static readonly Participant NULL_PARTICIPANT = new ();

        public event ParticipantDelegate? UpdatesFromParticipant;

        public Participant LocalParticipant() =>
            NULL_PARTICIPANT;

        public Participant? RemoteParticipant(string identity) =>
            null;

        public IReadOnlyDictionary<string, Participant> RemoteParticipantIdentities() =>
            EMPTY_DICTIONARY;
    }
}
