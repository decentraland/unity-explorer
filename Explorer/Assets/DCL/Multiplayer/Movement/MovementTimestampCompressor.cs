using System;
using UnityEngine;
using Utility;

namespace DCL.Multiplayer.Movement.Systems
{
    [Serializable]
    public struct CompressedNetworkMovementMessage
    {
        public long compressedData;
        public Vector3 remainedPosition;

        public NetworkMovementMessage message;
    }

    public static class TimestampEncoder
    {
        public const int TIMESTAMP_BITS = 256; // 128, 512

        public const float ROUND_BUFFER = TIMESTAMP_BITS * SENT_INTERVAL;
        private const float SENT_INTERVAL = 0.1f; // == QUANTUM in this case

        public static long Compress(float timestamp, int bits = 8)
        {
            int steps = TIMESTAMP_BITS; // (int)Math.Pow(2, bits);
            float normalizedTimestamp = timestamp % ROUND_BUFFER; // Normalize timestamp within the round buffer
            return Mathf.RoundToInt(normalizedTimestamp / SENT_INTERVAL) % steps;
        }

        public static float Decompress(long data, int steps = TIMESTAMP_BITS)
        {
            var bits = (int)Math.Log(steps, 2);
            int mask = (1 << bits) - 1;
            var compressedTimestamp = (int)(data & mask);
            return compressedTimestamp * SENT_INTERVAL % ROUND_BUFFER;
        }
    }

    public static class ParcelEncoder
    {
        public const int PARCEL_SIZE = 16;

        // TODO (Vit): now hardcoded, but it should depend on the Landscape margins and Genesis Parcel data
        private const int MIN_X = -152;
        private const int MAX_X = 164;

        private const int MIN_Y = -152;

        // private const int MAX_Y = 160;

        private const int WIDTH = MAX_X - MIN_X + 1;

        public static int EncodeParcel(Vector2Int parcel) =>
            parcel.x - MIN_X + ((parcel.y - MIN_Y) * WIDTH);

        public static Vector2Int DecodeParcel(int index) =>
            new ((index % WIDTH) + MIN_X, (index / WIDTH) + MIN_Y);
    }

    public static class RelativePositionEncoder
    {
        public static int CompressScaledInteger(float value)
        {
            if (value < 0) value = 0;
            if (value >= 16) value = 255; // Represent the upper bound as 255

            var compressed = (int)(value / 16.0f * 255.0f); // Scale the value to the [0, 255] range
            return compressed & 0xFF; // Ensure it's within 8 bits
        }

        public static float DecompressScaledInteger(int compressed)
        {
            float value = compressed / 255.0f * 16.0f; // Scale back to the [0, 16) range
            return value;
        }

        // Not smooth
        // public const int mantissaBits = 5;
        // public const int exponentBits = 3;
        // public const int bias = 3;
        // public static int CompressFloatingPoint(float value)
        // {
        //     if (value <= 0) return 0;
        //     if (value >= 16) return 255;  // Adjust to 16 instead of 15
        //
        //     int exponent = 0;
        //
        //     // Normalize the value to be within the range [1, 2)
        //     while (value >= 2.0f && exponent < 7)
        //     {
        //         value /= 2.0f;
        //         exponent++;
        //     }
        //
        //     exponent = exponent + bias; // Apply bias
        //
        //     int mantissa = (int)((value - 1.0f) * (1 << mantissaBits) + 0.5f); // Rounding to reduce errors
        //     int compressed = (exponent << mantissaBits) | mantissa;
        //     return compressed & 0xFF; // Ensure it's within 8 bits
        // }
        //
        // public static float DecompressFloatingPoint(int compressed)
        // {
        //     int exponent = ((compressed >> mantissaBits) & ((1 << exponentBits) - 1)) - bias;
        //     int mantissa = compressed & ((1 << mantissaBits) - 1);
        //
        //     float value = 1.0f + ((float)mantissa / (1 << mantissaBits));
        //     value *= (1 << exponent);
        //     return value;
        // }
    }

    public static class NetworkMessageCompressor
    {
        public static CompressedNetworkMovementMessage Compress(this NetworkMovementMessage message)
        {
            Vector2Int parcel = message.position.ToParcel();

            long compressedTimestamp = TimestampEncoder.Compress(message.timestamp); // Encode timestamp (8 bits)
            int parcelIndex = ParcelEncoder.EncodeParcel(parcel); // Encode parcel (17 bits)

            // Compress relative X and Y (up to 16 bits each)
            var relativePosition = new Vector2(
                message.position.x - (parcel.x * ParcelEncoder.PARCEL_SIZE),
                message.position.z - (parcel.y * ParcelEncoder.PARCEL_SIZE) // Y is Z in this case
            );

            int compressedX = RelativePositionEncoder.CompressScaledInteger(relativePosition.x);
            int compressedZ = RelativePositionEncoder.CompressScaledInteger(relativePosition.y);

            var timestampShift = (int)Math.Log(TimestampEncoder.TIMESTAMP_BITS, 2);

            return new CompressedNetworkMovementMessage
            {
                compressedData = compressedTimestamp
                                 | ((long)parcelIndex << timestampShift)
                                 | ((long)compressedX << (timestampShift + 17))
                                 | ((long)compressedZ << (timestampShift + 25)),

                remainedPosition = new Vector3(relativePosition.x, message.position.y, relativePosition.y),
                message = message,
            };
        }

        public static NetworkMovementMessage Decompress(this CompressedNetworkMovementMessage compressedMessage)
        {
            long data = compressedMessage.compressedData;

            float timestamp = TimestampEncoder.Decompress(data);
            Vector2Int parcel = ParcelEncoder.DecodeParcel((int)((data >> 8) & 0x1FFFF)); // Extract parcel index (17 bits)

            // Decompressing values
            int extractedX = (int)((data >> 25) & 0xFF);
            int extractedZ = (int)((data >> 33) & 0xFF);

            float decompressedX = RelativePositionEncoder.DecompressScaledInteger(extractedX);
            float decompressedZ = RelativePositionEncoder.DecompressScaledInteger(extractedZ);

            Debug.Log($"VVV {compressedMessage.remainedPosition.x} - {decompressedX} | {compressedMessage.remainedPosition.z} - {decompressedZ}");

            var worldPosition = new Vector3(
                (parcel.x * ParcelEncoder.PARCEL_SIZE) + decompressedX,
                compressedMessage.remainedPosition.y,
                (parcel.y * ParcelEncoder.PARCEL_SIZE) + decompressedZ
            );

            return new NetworkMovementMessage
            {
                timestamp = timestamp,
                position = worldPosition,
                velocity = compressedMessage.message.velocity,
                animState = compressedMessage.message.animState,
                isStunned = compressedMessage.message.isStunned,
            };
        }
    }
}
