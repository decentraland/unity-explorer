using DCL.Diagnostics;
using LiveKit.Rooms.Participants;
using System;
using System.Collections.Generic;

namespace DCL.Multiplayer.Connections.Rooms.Logs
{
    public class LogParticipantsHub : IParticipantsHub
    {
        private const string PREFIX = "LogParticipantsHub:";
        private readonly IParticipantsHub origin;
        private readonly Action<string> log;

        public event ParticipantDelegate? UpdatesFromParticipant;

        public LogParticipantsHub(IParticipantsHub origin) : this(
            origin,
            ReportHub
               .WithReport(ReportCategory.LIVEKIT)
               .Log
        ) { }

        public LogParticipantsHub(IParticipantsHub origin, Action<string> log)
        {
            this.origin = origin;
            this.log = log;
            origin.UpdatesFromParticipant += OriginOnUpdatesFromParticipant;
        }

        private void OriginOnUpdatesFromParticipant(Participant participant, UpdateFromParticipant update)
        {
            log($"{PREFIX} updates from participant {participant} - {update}");
            UpdatesFromParticipant?.Invoke(participant, update);
        }

        public Participant LocalParticipant()
        {
            Participant participant = origin.LocalParticipant();
            log($"{PREFIX} local {participant.ReadableString()}");
            return participant;
        }

        public Participant? RemoteParticipant(string identity)
        {
            Participant? participant = origin.RemoteParticipant(identity);
            log($"{PREFIX} with sid {identity} remote {participant?.ReadableString() ?? "NONE"}");
            return participant;
        }

        public IReadOnlyCollection<string> RemoteParticipantIdentities()
        {
            IReadOnlyCollection<string> sids = origin.RemoteParticipantIdentities();
            log($"{PREFIX} remote sids {(sids.Count > 0 ? string.Join(", ", sids) : "empty")} ");
            return sids;
        }
    }
}
