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

        public int Count
        {
            get
            {
                int count = origin.Count;
                ReportHub
                   .WithReport(ReportCategory.LIVEKIT)
                   .Log($"{PREFIX} count {count}");
                return count;
            }
        }

        public event Action? Updated;

        public LogActiveSpeakers(IActiveSpeakers origin)
        {
            this.origin = origin;
            origin.Updated += OriginOnUpdated;
        }

        private void OriginOnUpdated()
        {
            ReportHub
               .WithReport(ReportCategory.LIVEKIT)
               .Log($"{PREFIX} updated");
            Updated?.Invoke();
        }

        public IEnumerator<string> GetEnumerator()
        {
            var count = 0;

            foreach (string? speaker in origin)
            {
                ReportHub
                   .WithReport(ReportCategory.LIVEKIT)
                   .Log($"{PREFIX} speaker:{count++} - {speaker}");
                yield return speaker;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() =>
            GetEnumerator();
    }
}
