using DCL.Diagnostics;
using LiveKit.Rooms.ActiveSpeakers;
using System;
using System.Collections;
using System.Collections.Generic;

namespace DCL.Multiplayer.Connections.Rooms.Logs
{
    public class LogActiveSpeakers : IActiveSpeakers
    {
        private const string PREFIX = "LogActiveSpeakers:";

        private readonly IActiveSpeakers origin;
        private readonly Action<string> log;

        public int Count
        {
            get
            {
                int count = origin.Count;
                log($"{PREFIX} count {count}");
                return count;
            }
        }

        public event Action? Updated;

        public LogActiveSpeakers(IActiveSpeakers origin) : this(origin, ReportHub.WithReport(ReportCategory.LIVEKIT).Log) { }

        public LogActiveSpeakers(IActiveSpeakers origin, Action<string> log)
        {
            this.origin = origin;
            this.log = log;
            origin.Updated += OriginOnUpdated;
        }

        private void OriginOnUpdated()
        {
            log($"{PREFIX} updated");
            Updated?.Invoke();
        }

        public IEnumerator<string> GetEnumerator()
        {
            var count = 0;

            foreach (string? speaker in origin)
            {
                log($"{PREFIX} speaker:{count++} - {speaker}");
                yield return speaker;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() =>
            GetEnumerator();
    }
}
