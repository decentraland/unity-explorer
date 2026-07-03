using DCL.Multiplayer.Movement.Settings;
using System;

namespace DCL.Multiplayer.Movement
{
    /// <summary>
    ///     Encode timepsamp as a circular buffer
    /// </summary>
    public class TimestampEncoder
    {
        private const double WRAPAROUND_THRESHOLD = 0.75;

        private readonly MessageEncodingSettings settings;

        private double lastOriginalTimestamp;
        private double timestampOffset;

        private int steps => 1 << settings.TIMESTAMP_BITS; // 2^TIMESTAMP_BITS
        private int mask => steps - 1;

        public double BufferSize => steps * (double)settings.TIMESTAMP_QUANTUM;

        public TimestampEncoder(MessageEncodingSettings settings)
        {
            this.settings = settings;
        }

        public int Compress(double timestamp)
        {
            double normalizedTimestamp = timestamp % BufferSize; // Normalize timestamp within the round buffer
            return (int)Math.Round(normalizedTimestamp / settings.TIMESTAMP_QUANTUM) % steps;
        }

        public double Decompress(long data)
        {
            double decompressedTimestamp = (int)(data & mask) * (double)settings.TIMESTAMP_QUANTUM % BufferSize;
            double adjustedTimestamp = decompressedTimestamp + timestampOffset;

            // Adjust to buffer wraparound
            if (adjustedTimestamp < lastOriginalTimestamp - (BufferSize * WRAPAROUND_THRESHOLD))
            {
                timestampOffset += BufferSize;
                adjustedTimestamp += BufferSize;
            }

            lastOriginalTimestamp = adjustedTimestamp;
            return adjustedTimestamp;
        }
    }
}
