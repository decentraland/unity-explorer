using DCL.CharacterMotion.Components;
using System;
using UnityEngine;
using Utility;
using static DCL.Multiplayer.Movement.Systems.CompressionConfig;

namespace DCL.Multiplayer.Movement.Systems
{
    public static class CompressionConfig
    {
        public const int PARCEL_SIZE = 16;

        public const int Y_MAX = 150;
        public const int MAX_VELOCITY = 10;

        public const float TIMESTAMP_QUANTUM = 0.01f;
        public const int TIMESTAMP_BITS = 22;

        public const int MOVEMENT_KIND_BITS = 2;
        public const int MOVEMENT_KIND_MASK = 0x3;

        // 22 + 2 + 7 = 25
        public const int MOVEMENT_KIND_START_BIT = TIMESTAMP_BITS;
        public const int SLIDING_BIT = MOVEMENT_KIND_START_BIT  + MOVEMENT_KIND_BITS;
        public const int STUNNED_BIT = SLIDING_BIT + 1;
        public const int GROUNDED_BIT = STUNNED_BIT + 1;
        public const int JUMPING_BIT = GROUNDED_BIT + 1;
        public const int LONG_JUMP_BIT = JUMPING_BIT + 1;
        public const int FALLING_BIT = LONG_JUMP_BIT + 1;
        public const int LONG_FALL_BIT = FALLING_BIT + 1;

        // 17 + 8 + 8 + 13 + 6 + 6 + 6 = 64
        public const int PARCEL_BITS = 17;
        public const int XZ_BITS = 8;
        public const int Y_BITS = 13;
        public const int VELOCITY_BITS = 6;
    }

    [Serializable]
    public struct CompressedNetworkMovementMessage
    {


        public int temporalData;
        public long movementData;

        // public NetworkMovementMessage message;
    }

    public static class TimestampEncoder
    {
        public static float Buffer => steps * TIMESTAMP_QUANTUM;

        private static int steps => (int)Math.Pow(2, TIMESTAMP_BITS); // 128, 256, 512

        public static int Compress(float timestamp)
        {
            float normalizedTimestamp = timestamp % Buffer; // Normalize timestamp within the round buffer
            return Mathf.RoundToInt(normalizedTimestamp / TIMESTAMP_QUANTUM) % steps;
        }

        public static float Decompress(long data, int bits)
        {
            int mask = (1 << bits) - 1;
            return (int)(data & mask) * TIMESTAMP_QUANTUM % Buffer;
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

    public static class VelocityEncoder
    {
        public static int CompressVelocity(float value, float maxValue, int bits)
        {
            int maxStep = (1 << bits) - 1;
            float normalizedValue = (value + maxValue) / (2 * maxValue); // Shift and scale to 0-1 range
            return Mathf.RoundToInt(Mathf.Clamp01(normalizedValue) * maxStep);
        }

        public static float DecompressVelocity(int compressed, float maxValue, int bits)
        {
            float maxStep = (1 << bits) - 1f;
            float normalizedValue = compressed / maxStep;
            return (normalizedValue * 2 * maxValue) - maxValue; // Rescale and shift back to original range
        }
    }

    public static class NetworkMessageEncoder
    {


        public static CompressedNetworkMovementMessage Compress(this NetworkMovementMessage message)
        {
            Vector2Int parcel = message.position.ToParcel();

            int compressedData = TimestampEncoder.Compress(message.timestamp);
            int parcelIndex = ParcelEncoder.EncodeParcel(parcel);

            var relativePosition = new Vector2(
                message.position.x - (parcel.x * ParcelEncoder.PARCEL_SIZE),
                message.position.z - (parcel.y * ParcelEncoder.PARCEL_SIZE) // Y is Z in this case
            );

            int compressedX = RelativePositionEncoder.CompressScaledInteger(relativePosition.x, PARCEL_SIZE, XZ_BITS);
            int compressedZ = RelativePositionEncoder.CompressScaledInteger(relativePosition.y, PARCEL_SIZE, XZ_BITS);
            int compressedY = RelativePositionEncoder.CompressScaledInteger(message.position.y, Y_MAX, Y_BITS);

            int compressedVelocityX = VelocityEncoder.CompressVelocity(message.velocity.x, MAX_VELOCITY, VELOCITY_BITS);
            int compressedVelocityY = VelocityEncoder.CompressVelocity(message.velocity.y, MAX_VELOCITY, VELOCITY_BITS);
            int compressedVelocityZ = VelocityEncoder.CompressVelocity(message.velocity.z, MAX_VELOCITY, VELOCITY_BITS);

            compressedData |= ((int)message.movementKind & MOVEMENT_KIND_MASK) << MOVEMENT_KIND_START_BIT;
            if (message.isSliding) compressedData |= 1 << SLIDING_BIT;
            if (message.isStunned) compressedData |= 1 << STUNNED_BIT;
            if (message.animState.IsGrounded) compressedData |= 1 << GROUNDED_BIT;
            if (message.animState.IsJumping) compressedData |= 1 << JUMPING_BIT;
            if (message.animState.IsLongJump) compressedData |= 1 << LONG_JUMP_BIT;
            if (message.animState.IsFalling) compressedData |= 1 << FALLING_BIT;
            if (message.animState.IsLongFall) compressedData |= 1 << LONG_FALL_BIT;

            return new CompressedNetworkMovementMessage
            {
                temporalData = compressedData,

                movementData = (uint)parcelIndex
                               | ((long)compressedX << PARCEL_BITS)
                               | ((long)compressedZ << (PARCEL_BITS + XZ_BITS))
                               | ((long)compressedY << (PARCEL_BITS + XZ_BITS + XZ_BITS))
                               | ((long)compressedVelocityX << (PARCEL_BITS + XZ_BITS + XZ_BITS + Y_BITS))
                               | ((long)compressedVelocityY << (PARCEL_BITS + XZ_BITS + XZ_BITS + Y_BITS + VELOCITY_BITS))
                               | ((long)compressedVelocityZ << (PARCEL_BITS + XZ_BITS + XZ_BITS + Y_BITS + VELOCITY_BITS + VELOCITY_BITS)),

                // message = message,
            };
        }

        public static NetworkMovementMessage Decompress(this CompressedNetworkMovementMessage compressedMessage)
        {
            int compressedTemporalData = compressedMessage.temporalData;
            float timestamp = TimestampEncoder.Decompress(compressedTemporalData, TIMESTAMP_BITS);

            long compressedMovement = compressedMessage.movementData;

            const int PARCEL_MASK = (1 << PARCEL_BITS) - 1;
            const int XZ_MASK = (1 << XZ_BITS) - 1;
            const int Y_MASK = (1 << Y_BITS) - 1;
            const int VELOCITY_MASK = (1 << VELOCITY_BITS) - 1;

            Vector2Int parcel = ParcelEncoder.DecodeParcel((int)(compressedMovement & PARCEL_MASK));

            // Decompressing values
            var extractedX = (int)((compressedMovement >> PARCEL_BITS) & XZ_MASK);
            var extractedZ = (int)((compressedMovement >> (PARCEL_BITS + XZ_BITS)) & XZ_MASK);
            var extractedY = (int)((compressedMovement >> (PARCEL_BITS + XZ_BITS + XZ_BITS)) & Y_MASK);

            float decompressedX = RelativePositionEncoder.DecompressScaledInteger(extractedX, PARCEL_SIZE, XZ_BITS);
            float decompressedZ = RelativePositionEncoder.DecompressScaledInteger(extractedZ, PARCEL_SIZE, XZ_BITS);
            float decompressedY = RelativePositionEncoder.DecompressScaledInteger(extractedY, Y_MAX, Y_BITS);

            var worldPosition = new Vector3(
                (parcel.x * ParcelEncoder.PARCEL_SIZE) + decompressedX,
                decompressedY,
                (parcel.y * ParcelEncoder.PARCEL_SIZE) + decompressedZ
            );

            var extractedVelocityX = (int)((compressedMovement >> (PARCEL_BITS + XZ_BITS + XZ_BITS + Y_BITS)) & VELOCITY_MASK);
            var extractedVelocityY = (int)((compressedMovement >> (PARCEL_BITS + XZ_BITS + XZ_BITS + Y_BITS + VELOCITY_BITS)) & VELOCITY_MASK);
            var extractedVelocityZ = (int)((compressedMovement >> (PARCEL_BITS + XZ_BITS + XZ_BITS + Y_BITS + VELOCITY_BITS + VELOCITY_BITS)) & VELOCITY_MASK);

            float decompressedVelocityX = VelocityEncoder.DecompressVelocity(extractedVelocityX, MAX_VELOCITY, VELOCITY_BITS);
            float decompressedVelocityY = VelocityEncoder.DecompressVelocity(extractedVelocityY, MAX_VELOCITY, VELOCITY_BITS);
            float decompressedVelocityZ = VelocityEncoder.DecompressVelocity(extractedVelocityZ, MAX_VELOCITY, VELOCITY_BITS);

            var velocity = new Vector3(decompressedVelocityX, decompressedVelocityY, decompressedVelocityZ);

            return new NetworkMovementMessage
            {
                timestamp = timestamp,
                position = worldPosition,
                velocity = velocity,
                movementKind = (MovementKind)((compressedTemporalData >> MOVEMENT_KIND_START_BIT) & MOVEMENT_KIND_MASK),

                animState = new AnimationStates
                {
                    MovementBlendValue = 0f,//compressedMessage.message.animState.MovementBlendValue,
                    SlideBlendValue = 0f, //compressedMessage.message.animState.SlideBlendValue,

                    IsGrounded = (compressedTemporalData & (1 << GROUNDED_BIT)) != 0,
                    IsJumping = (compressedTemporalData & (1 << JUMPING_BIT)) != 0,
                    IsLongJump = (compressedTemporalData & (1 << LONG_JUMP_BIT)) != 0,
                    IsFalling = (compressedTemporalData & (1 << FALLING_BIT)) != 0,
                    IsLongFall = (compressedTemporalData & (1 << LONG_FALL_BIT)) != 0,
                },

                isStunned = (compressedTemporalData & (1 << STUNNED_BIT)) != 0,
                isSliding = (compressedTemporalData & (1 << SLIDING_BIT)) != 0,
            };
        }
    }
}
