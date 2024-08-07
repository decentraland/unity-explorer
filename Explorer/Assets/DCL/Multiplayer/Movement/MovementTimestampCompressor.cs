using System;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Systems
{
    [Serializable]
    public struct CompressedNetworkMovementMessage
    {
        public long compressedData;
        public NetworkMovementMessage message;
    }

    public static class NetworkMessageCompressor
    {
        private const float ROUND_BUFFER_SECONDS = 10f;
        private const int STEPS8_BIT = 256;
        private const int STEPS9_BIT = 512;
        private const float QUANTUM_SECONDS8_BIT = ROUND_BUFFER_SECONDS / STEPS8_BIT;
        private const float QUANTUM_SECONDS9_BIT = ROUND_BUFFER_SECONDS / STEPS9_BIT;

        public static CompressedNetworkMovementMessage Compress(this NetworkMovementMessage message, int steps = STEPS9_BIT)
        {
            // Compress timestamp
            float quantumSeconds = ROUND_BUFFER_SECONDS / steps;  // 19.53 ms for 9Bit; 39.06 ms for 8Bit
            float normalizedTimestamp = message.timestamp % ROUND_BUFFER_SECONDS; // Normalize timestamp within the round buffer
            long compressedTimestamp = Mathf.RoundToInt(normalizedTimestamp / quantumSeconds) % steps;

            return new CompressedNetworkMovementMessage
            {
                compressedData = compressedTimestamp,
                message = message,
            };
        }

        public static NetworkMovementMessage Decompress(this CompressedNetworkMovementMessage compressedMessage, int steps = STEPS9_BIT)
        {
            bool use9Bit = steps == STEPS9_BIT;
            int mask = use9Bit ? 0x1FF : 0xFF;
            float quantum = use9Bit ? QUANTUM_SECONDS9_BIT : QUANTUM_SECONDS8_BIT;

            var compressedTimestamp = (int)(compressedMessage.compressedData & mask);
            // Debug.Assert(quantum == RoundBufferSeconds / steps, "VVV should be equal");
            float timestamp = (compressedTimestamp * quantum) % ROUND_BUFFER_SECONDS;

            return new NetworkMovementMessage
            {
                timestamp = timestamp,
                position = compressedMessage.message.position,
                velocity = compressedMessage.message.velocity,
                animState = compressedMessage.message.animState,
                isStunned = compressedMessage.message.isStunned,
            };
        }
    }
}
