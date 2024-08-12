using System;
using UnityEngine;
using Utility;
using static DCL.Multiplayer.Movement.Systems.CompressedNetworkMovementMessage;

namespace DCL.Multiplayer.Movement.Systems
{

    [Serializable]
    public struct CompressedNetworkMovementMessage
    {
        public const int  TIMESTAMP_BITS = 8;
        public const int PARCEL_SIZE = 16;
        public const int Y_MAX = 150;

        public const int  PARCEL_BITS = 17;
        public const int  XZ_BITS = 8;
        public const int  Y_BITS = 11;

        public long compressedData;

        public NetworkMovementMessage message;
    }

    public static class TimestampEncoder
    {
        private const float SENT_INTERVAL = 0.1f; // == QUANTUM in this case

        private static int steps =>  (int)Math.Pow(2, TIMESTAMP_BITS); // 128, 256, 512
        public static float Buffer => steps * SENT_INTERVAL;

        public static long Compress(float timestamp)
        {
            float normalizedTimestamp = timestamp % Buffer; // Normalize timestamp within the round buffer
            return Mathf.RoundToInt(normalizedTimestamp / SENT_INTERVAL) % steps;
        }

        public static float Decompress(long data, int bits)
        {
            int mask = (1 << bits) - 1;
            return (int)(data & mask) * SENT_INTERVAL % Buffer;
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
        public static int CompressScaledInteger(float value, int maxValue, int bits)
        {
            int maxStep = (1 << bits) - 1;
            return Mathf.RoundToInt(Mathf.Clamp01(value / maxValue) * maxStep);
        }

        public static float DecompressScaledInteger(int compressed, int maxValue, int bits)
        {
            float maxStep = (1 << bits) - 1f;
            return compressed / maxStep * maxValue;
        }
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

            int compressedX = RelativePositionEncoder.CompressScaledInteger(relativePosition.x, PARCEL_SIZE, XZ_BITS);
            int compressedZ = RelativePositionEncoder.CompressScaledInteger(relativePosition.y, PARCEL_SIZE, XZ_BITS);
            int compressedY = RelativePositionEncoder.CompressScaledInteger(message.position.y, Y_MAX, Y_BITS);

            return new CompressedNetworkMovementMessage
            {
                compressedData = compressedTimestamp
                                 | ((long)parcelIndex << TIMESTAMP_BITS)
                                 | ((long)compressedX << (TIMESTAMP_BITS + PARCEL_BITS))
                                 | ((long)compressedZ << (TIMESTAMP_BITS + PARCEL_BITS + XZ_BITS))
                                 | ((long)compressedY << (TIMESTAMP_BITS + PARCEL_BITS + XZ_BITS + XZ_BITS)),

                message = message,
            };
        }

        public static NetworkMovementMessage Decompress(this CompressedNetworkMovementMessage compressedMessage)
        {
            long data = compressedMessage.compressedData;

            const int PARCEL_MASK = (1 << PARCEL_BITS) - 1;
            const int XZ_MASK = (1 << XZ_BITS) - 1;
            const int Y_MASK = (1 << Y_BITS) - 1;

            float timestamp = TimestampEncoder.Decompress(data, TIMESTAMP_BITS);
            Vector2Int parcel = ParcelEncoder.DecodeParcel((int)((data >> TIMESTAMP_BITS) & PARCEL_MASK)); // Extract parcel index (17 bits)

            // Decompressing values
            var extractedX = (int)((data >> (TIMESTAMP_BITS + PARCEL_BITS)) & XZ_MASK);
            var extractedZ = (int)((data >> (TIMESTAMP_BITS + PARCEL_BITS + XZ_BITS)) & XZ_MASK);
            var extractedY = (int)((data >> (TIMESTAMP_BITS + PARCEL_BITS + XZ_BITS + XZ_BITS)) & Y_MASK);

            float decompressedX = RelativePositionEncoder.DecompressScaledInteger(extractedX, PARCEL_SIZE, XZ_BITS);
            float decompressedZ = RelativePositionEncoder.DecompressScaledInteger(extractedZ, PARCEL_SIZE, XZ_BITS);
            float decompressedY = RelativePositionEncoder.DecompressScaledInteger(extractedY, Y_MAX, Y_BITS);

            var worldPosition = new Vector3(
                (parcel.x * ParcelEncoder.PARCEL_SIZE) + decompressedX,
                compressedMessage.message.position.y, //decompressedY,
                (parcel.y * ParcelEncoder.PARCEL_SIZE) + decompressedZ
            );

            Debug.Log($"VVV {compressedMessage.message.position.y} - {decompressedY}");

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
