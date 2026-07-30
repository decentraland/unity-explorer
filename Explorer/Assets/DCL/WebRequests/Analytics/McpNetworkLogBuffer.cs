using System;
using System.Collections.Generic;

namespace DCL.WebRequests.Analytics
{
    /// <summary>
    ///     Thread-safe ring buffer of completed HTTP requests, keyed by monotonic sequence number. Mirrors
    ///     <c>DCL.McpServer.Utils.SceneLogBuffer</c>: storage is kept separate from the
    ///     <see cref="McpNetworkAnalyticsHandler" /> that feeds it so this class can be unit-tested in isolation.
    /// </summary>
    public sealed class McpNetworkLogBuffer
    {
        private const int CAPACITY = 512;

        private readonly object gate = new ();
        private readonly Entry[] entries = new Entry[CAPACITY];

        private long nextSeq;

        /// <summary>Sequence number of the newest stored entry, or -1 when empty.</summary>
        public long LatestSeq
        {
            get
            {
                lock (gate) { return nextSeq - 1; }
            }
        }

        /// <summary>
        ///     Records a finished request. Writers are the web-request analytics callbacks — the main thread for a
        ///     completed request, whichever thread the continuation failed on for an exception — while
        ///     <see cref="CopyTo" /> is served on the MCP transport's thread pool, so every access takes the lock.
        /// </summary>
        public void Append(string url, string method, int status, string mimeType, long sizeBytes, double durationMs, bool failed, string? failureReason)
        {
            lock (gate)
            {
                entries[nextSeq % CAPACITY] = new Entry(nextSeq, DateTime.UtcNow, url, method, status, mimeType, sizeBytes, durationMs, failed, failureReason);
                nextSeq++;
            }
        }

        /// <summary>
        ///     Copies up to <paramref name="limit" /> newest entries with Seq greater than <paramref name="sinceSeq" />
        ///     into <paramref name="target" /> in chronological order. When <paramref name="failedOnly" /> is set only
        ///     unsuccessful entries survive (a transport failure or an HTTP status >= 400); when
        ///     <paramref name="status" /> is non-negative only entries with that exact HTTP status survive.
        /// </summary>
        public void CopyTo(List<Entry> target, long sinceSeq, bool failedOnly, int status, int limit)
        {
            lock (gate)
            {
                // sinceSeq arrives unvalidated from the caller: leave before sinceSeq + 1 can wrap past long.MaxValue
                // into long.MinValue and turn a request for nothing into a copy of the entire ring.
                if (sinceSeq >= nextSeq)
                    return;

                long oldestAvailable = nextSeq >= CAPACITY ? nextSeq - CAPACITY : 0;
                long from = sinceSeq + 1 > oldestAvailable ? sinceSeq + 1 : oldestAvailable;

                int firstIndex = target.Count;
                int added = 0;

                // Newest first, stopping at limit: collecting the whole ring first would grow the caller's pooled
                // list far past the requested size, and it happens while every Append is blocked on the lock.
                for (long seq = nextSeq - 1; seq >= from && added < limit; seq--)
                {
                    Entry entry = entries[seq % CAPACITY];

                    if (failedOnly && !entry.IsUnsuccessful)
                        continue;

                    if (status >= 0 && entry.Status != status)
                        continue;

                    target.Add(entry);
                    added++;
                }

                target.Reverse(firstIndex, added);
            }
        }

        public readonly struct Entry
        {
            public readonly long Seq;
            public readonly DateTime TimestampUtc;
            public readonly string Url;
            public readonly string Method;
            public readonly int Status;
            public readonly string MimeType;
            public readonly long SizeBytes;
            public readonly double DurationMs;
            public readonly bool Failed;
            public readonly string? FailureReason;

            public Entry(long seq, DateTime timestampUtc, string url, string method, int status, string mimeType, long sizeBytes, double durationMs, bool failed, string? failureReason)
            {
                Seq = seq;
                TimestampUtc = timestampUtc;
                Url = url;
                Method = method;
                Status = status;
                MimeType = mimeType;
                SizeBytes = sizeBytes;
                DurationMs = durationMs;
                Failed = failed;
                FailureReason = failureReason;
            }

            /// <summary>A transport failure, or an HTTP response that carried an error status.</summary>
            public bool IsUnsuccessful => Failed || Status >= 400;
        }
    }
}
