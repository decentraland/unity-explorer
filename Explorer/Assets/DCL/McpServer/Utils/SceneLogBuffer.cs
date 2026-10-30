using DCL.Diagnostics;
using DCL.UI.DebugMenu.LogHistory;
using System.Collections.Generic;

namespace DCL.McpServer.Utils
{
    /// <summary>
    ///     Who produced a buffered log line: the scene's own JavaScript, or the client (its errors, and the
    ///     automation categories' own lines).
    /// </summary>
    public enum SceneLogSource : byte
    {
        Scene,
        Client,
    }

    /// <summary>
    ///     Thread-safe ring buffer of console log entries with monotonic sequence numbers, so agents can poll
    ///     incrementally via sinceSeq. Fed by a <see cref="ReportSinkHandler" />, so each entry keeps its report
    ///     category and the scene it was reported for.
    /// </summary>
    public class SceneLogBuffer : IReportSink
    {
        private const int CAPACITY = 1000;

        private readonly object gate = new ();
        private readonly Entry[] entries = new Entry[CAPACITY];

        private long nextSeq;

        /// <summary>
        ///     Sequence number of the newest stored entry, or -1 when empty.
        /// </summary>
        public long LatestSeq
        {
            get
            {
                lock (gate) { return nextSeq - 1; }
            }
        }

        /// <summary>
        ///     May be invoked from any thread.
        /// </summary>
        public void Receive(in ReportLogEntry report)
        {
            // The console entry's formatting (timestamp and level prefix) keeps the lines identical to the scene debug console's.
            DebugMenuConsoleLogEntry consoleEntry = DebugMenuConsoleLogEntry.FromUnityLog(report.LogType, report.Message);
            string message = string.IsNullOrEmpty(report.StackTrace) ? consoleEntry.Message : $"{consoleEntry.Message}\n{report.StackTrace}";
            SceneLogSource source = report.Category == ReportCategory.JAVASCRIPT ? SceneLogSource.Scene : SceneLogSource.Client;
            string? sceneName = report.Scene.Equals(default(SceneShortInfo)) ? null : report.Scene.Name;

            lock (gate)
            {
                entries[nextSeq % CAPACITY] = new Entry(nextSeq, consoleEntry.Type, source, report.Category, sceneName, message);
                nextSeq++;
            }
        }

        /// <summary>
        ///     Copies up to <paramref name="limit" /> newest entries with Seq greater than <paramref name="sinceSeq" />
        ///     into <paramref name="target" /> in chronological order. Client entries are skipped unless
        ///     <paramref name="includeClient" />; sequence numbers are never renumbered, so a skipped entry still
        ///     advances the seq a poller sees.
        /// </summary>
        public void CopyTo(List<Entry> target, long sinceSeq, bool errorsOnly, bool includeClient, int limit)
        {
            lock (gate)
            {
                long oldestAvailable = nextSeq >= CAPACITY ? nextSeq - CAPACITY : 0;
                long from = sinceSeq + 1 > oldestAvailable ? sinceSeq + 1 : oldestAvailable;

                for (long seq = from; seq < nextSeq; seq++)
                {
                    Entry entry = entries[seq % CAPACITY];

                    if (errorsOnly && entry.Type != LogMessageType.Error)
                        continue;

                    if (!includeClient && entry.Source == SceneLogSource.Client)
                        continue;

                    target.Add(entry);
                }

                if (target.Count > limit)
                    target.RemoveRange(0, target.Count - limit);
            }
        }

        public readonly struct Entry
        {
            public readonly long Seq;
            public readonly LogMessageType Type;
            public readonly SceneLogSource Source;
            public readonly string Category;
            public readonly string? SceneName;
            public readonly string Message;

            public Entry(long seq, LogMessageType type, SceneLogSource source, string category, string? sceneName, string message)
            {
                Seq = seq;
                Type = type;
                Source = source;
                Category = category;
                SceneName = sceneName;
                Message = message;
            }
        }
    }
}
