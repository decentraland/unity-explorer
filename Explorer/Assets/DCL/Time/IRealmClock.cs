using System;
using System.Globalization;
using System.Threading;

namespace DCL.Time
{
    /// <summary>
    ///     Provides server-aligned UTC time. The offset is fed by the realm <c>/about</c> response.
    /// </summary>
    public sealed class RealmClock
    {
        private long offsetTicks;
        private int hasSample;

        /// <summary>Server-aligned current UTC time. Null until the first sample is recorded.</summary>
        public DateTime? UtcNow => Volatile.Read(ref hasSample) == 1
            ? DateTime.UtcNow + new TimeSpan(Volatile.Read(ref offsetTicks))
            : null;

        /// <summary>Records a server UTC sample.</summary>
        public void RecordServerTime(DateTime serverUtc)
        {
            DateTime serverUtcNorm = serverUtc.Kind == DateTimeKind.Utc
                ? serverUtc
                : serverUtc.ToUniversalTime();

            TimeSpan delta = serverUtcNorm - DateTime.UtcNow;
            Interlocked.Exchange(ref offsetTicks, delta.Ticks);
            Interlocked.Exchange(ref hasSample, 1);
        }

        /// <summary>
        ///     Parses an HTTP-date value (RFC 1123 / RFC 7231) and records it as a server-time sample.
        /// </summary>
        public bool TryRecordHttpDate(string? httpDateHeader)
        {
            if (string.IsNullOrEmpty(httpDateHeader))
                return false;

            if (!DateTime.TryParse(
                    httpDateHeader,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out DateTime serverUtc))
                return false;

            RecordServerTime(serverUtc);
            return true;
        }
    }
}