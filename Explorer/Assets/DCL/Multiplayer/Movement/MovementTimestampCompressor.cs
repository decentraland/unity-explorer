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
        // private const float ROUND_BUFFER_SECONDS = 51.2f;
        public const int STEPS7_BIT = 128;
        public const int STEPS8_BIT = 256;
        public const int STEPS9_BIT = 512;

        public const float SENT_INTERVAL = 0.1f; // == QUANTUM in this case


        public const float ROUND_BUFFER = STEPS7_BIT * SENT_INTERVAL;

        public static CompressedNetworkMovementMessage Compress(this NetworkMovementMessage message, int steps = STEPS7_BIT)
        {
            float normalizedTimestamp = message.timestamp % ROUND_BUFFER; // Normalize timestamp within the round buffer
            long compressedTimestamp = Mathf.RoundToInt(normalizedTimestamp / SENT_INTERVAL) % steps;

            return new CompressedNetworkMovementMessage
            {
                compressedData = compressedTimestamp,
                message = message,
            };
        }

        public static NetworkMovementMessage Decompress(this CompressedNetworkMovementMessage compressedMessage, int steps = STEPS7_BIT)
        {
            int mask = steps == STEPS9_BIT ? 0x1FF :
                steps == STEPS8_BIT ? 0xFF : 0x7F;

            var compressedTimestamp = (int)(compressedMessage.compressedData & mask);

            // Debug.Assert(quantum == RoundBufferSeconds / steps, "VVV should be equal");
            float timestamp = compressedTimestamp * SENT_INTERVAL % ROUND_BUFFER;

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
