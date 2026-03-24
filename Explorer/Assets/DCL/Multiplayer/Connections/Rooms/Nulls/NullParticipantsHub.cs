using LiveKit.Rooms.Participants;
using System;
using System.Collections.Generic;

namespace DCL.Multiplayer.Connections.Rooms.Nulls
{
    public class NullParticipantsHub : IParticipantsHub
    {
        private static readonly IReadOnlyDictionary<string, LKParticipant> EMPTY_DICTIONARY = new Dictionary<string, LKParticipant>();

        public static readonly NullParticipantsHub INSTANCE = new ();
#if !UNITY_WEBGL || UNITY_EDITOR
        public static readonly WeakReference<LKParticipant> WEAK_NULL_PARTICIPANT = new (NULL_PARTICIPANT);
        private static readonly LKParticipant NULL_PARTICIPANT = new ();
#endif

        public event ParticipantDelegate? UpdatesFromParticipant;

        public LKParticipant LocalParticipant() =>
#if !UNITY_WEBGL || UNITY_EDITOR
            NULL_PARTICIPANT;
#else
            new LKParticipant();
#endif

        public LKParticipant? RemoteParticipant(string identity) =>
            null;

        public IReadOnlyDictionary<string, LKParticipant> RemoteParticipantIdentities() =>
            EMPTY_DICTIONARY;
    }
}
