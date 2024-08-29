using DCL.Multiplayer.Movement.Settings;
using UnityEngine;

namespace DCL.Multiplayer.Movement
{
    /// <summary>
    ///     Encode timepsamp as a circular buffer
    /// </summary>
    public class TimestampEncoder
    {
        private const float WRAPAROUND_THRESHOLD = 0.75f;

        private readonly MessageEncodingSettings settings;

        private float lastOriginalTimestamp;
        private float timestampOffset;

        private int steps => 1 << settings.TIMESTAMP_BITS; // 2^TIMESTAMP_BITS
        private int mask => steps - 1;

        public float BufferSize => steps * settings.TIMESTAMP_QUANTUM;

        public TimestampEncoder(MessageEncodingSettings settings)
        {
            this.settings = settings;
        }

        public int Compress(float timestamp)
        {
            float normalizedTimestamp = timestamp % BufferSize; // Normalize timestamp within the round buffer
            return Mathf.RoundToInt(normalizedTimestamp / settings.TIMESTAMP_QUANTUM) % steps;
        }

        public float Decompress(long data)
        {
            float decompressedTimestamp = (int)(data & mask) * settings.TIMESTAMP_QUANTUM % BufferSize;
            float adjustedTimestamp = decompressedTimestamp + timestampOffset;

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
