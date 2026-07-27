using DCL.UI.DebugMenu.LogHistory;
using System.Collections.Generic;

namespace DCL.McpServer.Utils
{
    /// <summary>
    ///     Thread-safe ring buffer of scene console log entries with monotonic sequence numbers,
    ///     so agents can poll incrementally via sinceSeq.
    /// </summary>
    public class SceneLogBuffer
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
        ///     Fed from <see cref="DCL.UI.DebugMenu.MessageBus.DebugMenuConsoleLogEntryBus" />; may be invoked from any thread.
        /// </summary>
        public void Append(DebugMenuConsoleLogEntry logEntry)
        {
            lock (gate)
            {
                entries[nextSeq % CAPACITY] = new Entry(nextSeq, logEntry.Type, logEntry.Message);
                nextSeq++;
            }
        }

        /// <summary>
        ///     Copies up to <paramref name="limit" /> newest entries with Seq greater than <paramref name="sinceSeq" />
        ///     into <paramref name="target" /> in chronological order.
        /// </summary>
        public void CopyTo(List<Entry> target, long sinceSeq, bool errorsOnly, int limit)
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
            public readonly string Message;

            public Entry(long seq, LogMessageType type, string message)
            {
                Seq = seq;
                Type = type;
                Message = message;
            }
        }
    }
}
