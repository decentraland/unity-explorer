using System;
using System.Diagnostics;
using System.Globalization;

namespace DCL.Time
{
    /// <summary>
    ///     Provides server-aligned UTC time. The clock is fed by the realm <c>/about</c> response (or earlier
    ///     trusted requests) and advances using a monotonic application clock — so it is unaffected by changes
    ///     to the local system clock after the first sample is recorded.
    /// </summary>
    public sealed class RealmClock
    {
        private DateTime sampleUtc;
        private long sampleTimestamp;
        private bool hasSample;

        /// <summary>True once a server-time sample has been recorded.</summary>
        public bool HasSample => hasSample;

        /// <summary>
        ///     Server-aligned current UTC time. Null until the first sample is recorded.
        ///     Advances with <see cref="Stopwatch"/>-based application time, not <see cref="DateTime.UtcNow"/>.
        /// </summary>
        public DateTime? UtcNow
        {
            get
            {
                if (!hasSample)
                    return null;

                long elapsedTicks = Stopwatch.GetTimestamp() - sampleTimestamp;
                return sampleUtc + TimeSpan.FromSeconds((double)elapsedTicks / Stopwatch.Frequency);
            }
        }

        /// <summary>Records a server UTC sample. Subsequent <see cref="UtcNow"/> reads advance from here.</summary>
        public void RecordServerTime(DateTime serverUtc)
        {
            sampleUtc = serverUtc.Kind == DateTimeKind.Utc
                ? serverUtc
                : serverUtc.ToUniversalTime();

            sampleTimestamp = Stopwatch.GetTimestamp();
            hasSample = true;
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