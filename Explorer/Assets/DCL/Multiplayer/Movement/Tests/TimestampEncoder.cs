using UnityEngine;

namespace TimestampEncodingTests
{
    public class TimestampEncoder
    {
        public const int BITS = 8; // Number of bits for timestamp
        public const float BUFFER = 10f; // 10 seconds buffer
        public const float QUANTUM = BUFFER / (1 << BITS); // Granularity per step: 10 / 256 = 0.0390625 seconds

        public static long Encode(float timestamp, bool isMoving)
        {
            long timestampMs = Mathf.FloorToInt((timestamp % BUFFER) / QUANTUM);

            // Ensure timestamp fits into 8 bits
            long encodedTimestamp = timestampMs & 0xFF; // Keep only 8 bits
            long stateBit = isMoving ? 1L : 0L;

            // Shift state bit to the most significant position, then place timestamp
            return (stateBit << 63) | (encodedTimestamp << (63 - BITS));
        }

        public static (float Timestamp, bool IsMoving) Decode(long encodedValue)
        {
            bool isMoving = (encodedValue & (1L << 63)) != 0;
            long timestamp = (encodedValue >> (63 - BITS)) & 0xFF; // Extract 8-bit timestamp

            // Convert timestamp back to seconds
            float timestampInSeconds = timestamp * QUANTUM;
            return (timestampInSeconds, isMoving);
        }
    }
}
