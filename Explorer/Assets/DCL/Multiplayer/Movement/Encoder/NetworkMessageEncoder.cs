using DCL.CharacterMotion.Components;
using UnityEngine;
using Utility;
using static DCL.Multiplayer.Movement.CompressionConfig;

namespace DCL.Multiplayer.Movement
{
    public static class NetworkMessageEncoder
    {
        public static CompressedNetworkMovementMessage Compress(this NetworkMovementMessage message) =>
            new()
            {
                temporalData = CompressTemporalData(message.timestamp, message.movementKind, message.isSliding, message.animState, message.isStunned),
                movementData = CompressMovementData(message.position, message.velocity),
            };

        private static int CompressTemporalData(float timestamp, MovementKind movementKind, bool isSliding, AnimationStates animState, bool isStunned)
        {
            int temporalData = TimestampEncoder.Compress(timestamp);

            // Animations
            temporalData |= ((int)movementKind & MOVEMENT_KIND_MASK) << MOVEMENT_KIND_START_BIT;
            if (isSliding) temporalData |= 1 << SLIDING_BIT;
            if (isStunned) temporalData |= 1 << STUNNED_BIT;
            if (animState.IsGrounded) temporalData |= 1 << GROUNDED_BIT;
            if (animState.IsJumping) temporalData |= 1 << JUMPING_BIT;
            if (animState.IsLongJump) temporalData |= 1 << LONG_JUMP_BIT;
            if (animState.IsFalling) temporalData |= 1 << FALLING_BIT;
            if (animState.IsLongFall) temporalData |= 1 << LONG_FALL_BIT;

            return temporalData;
        }

        private static long CompressMovementData(Vector3 position, Vector3 velocity)
        {
            Vector2Int parcel = position.ToParcel();

            int parcelIndex = ParcelEncoder.EncodeParcel(parcel);

            var relativePosition = new Vector2(
                position.x - (parcel.x * ParcelEncoder.PARCEL_SIZE),
                position.z - (parcel.y * ParcelEncoder.PARCEL_SIZE) // Y is Z in this case
            );

            int compressedX = FloatQuantizer.Compress(relativePosition.x, 0, PARCEL_SIZE, XZ_BITS);
            int compressedZ = FloatQuantizer.Compress(relativePosition.y, 0, PARCEL_SIZE, XZ_BITS);
            int compressedY = FloatQuantizer.Compress(position.y, 0, Y_MAX, Y_BITS);

            int compressedVelocityX = FloatQuantizer.Compress(velocity.x, -MAX_VELOCITY, MAX_VELOCITY, VELOCITY_BITS);
            int compressedVelocityY = FloatQuantizer.Compress(velocity.y, -MAX_VELOCITY, MAX_VELOCITY, VELOCITY_BITS);
            int compressedVelocityZ = FloatQuantizer.Compress(velocity.z, -MAX_VELOCITY, MAX_VELOCITY, VELOCITY_BITS);

            return (uint)parcelIndex
                   | ((long)compressedX << PARCEL_BITS)
                   | ((long)compressedZ << (PARCEL_BITS + XZ_BITS))
                   | ((long)compressedY << (PARCEL_BITS + XZ_BITS + XZ_BITS))
                   | ((long)compressedVelocityX << (PARCEL_BITS + XZ_BITS + XZ_BITS + Y_BITS))
                   | ((long)compressedVelocityY << (PARCEL_BITS + XZ_BITS + XZ_BITS + Y_BITS + VELOCITY_BITS))
                   | ((long)compressedVelocityZ << (PARCEL_BITS + XZ_BITS + XZ_BITS + Y_BITS + VELOCITY_BITS + VELOCITY_BITS));
        }

        public static NetworkMovementMessage Decompress(this CompressedNetworkMovementMessage compressedMessage)
        {
            long compressedMovement = compressedMessage.movementData;

            const int PARCEL_MASK = (1 << PARCEL_BITS) - 1;
            const int XZ_MASK = (1 << XZ_BITS) - 1;
            const int Y_MASK = (1 << Y_BITS) - 1;
            const int VELOCITY_MASK = (1 << VELOCITY_BITS) - 1;

            Vector2Int parcel = ParcelEncoder.DecodeParcel((int)(compressedMovement & PARCEL_MASK));

            var extractedX = (int)((compressedMovement >> PARCEL_BITS) & XZ_MASK);
            var extractedZ = (int)((compressedMovement >> (PARCEL_BITS + XZ_BITS)) & XZ_MASK);
            var extractedY = (int)((compressedMovement >> (PARCEL_BITS + XZ_BITS + XZ_BITS)) & Y_MASK);

            float decompressedX = FloatQuantizer.Decompress(extractedX, 0, PARCEL_SIZE, XZ_BITS);
            float decompressedZ = FloatQuantizer.Decompress(extractedZ, 0, PARCEL_SIZE, XZ_BITS);
            float decompressedY = FloatQuantizer.Decompress(extractedY, 0, Y_MAX, Y_BITS);

            var worldPosition = new Vector3(
                (parcel.x * ParcelEncoder.PARCEL_SIZE) + decompressedX,
                decompressedY,
                (parcel.y * ParcelEncoder.PARCEL_SIZE) + decompressedZ
            );

            var extractedVelocityX = (int)((compressedMovement >> (PARCEL_BITS + XZ_BITS + XZ_BITS + Y_BITS)) & VELOCITY_MASK);
            var extractedVelocityY = (int)((compressedMovement >> (PARCEL_BITS + XZ_BITS + XZ_BITS + Y_BITS + VELOCITY_BITS)) & VELOCITY_MASK);
            var extractedVelocityZ = (int)((compressedMovement >> (PARCEL_BITS + XZ_BITS + XZ_BITS + Y_BITS + VELOCITY_BITS + VELOCITY_BITS)) & VELOCITY_MASK);

            float decompressedVelocityX = FloatQuantizer.Decompress(extractedVelocityX, -MAX_VELOCITY, MAX_VELOCITY, VELOCITY_BITS);
            float decompressedVelocityY = FloatQuantizer.Decompress(extractedVelocityY, -MAX_VELOCITY, MAX_VELOCITY, VELOCITY_BITS);
            float decompressedVelocityZ = FloatQuantizer.Decompress(extractedVelocityZ, -MAX_VELOCITY, MAX_VELOCITY, VELOCITY_BITS);

            var velocity = new Vector3(decompressedVelocityX, decompressedVelocityY, decompressedVelocityZ);

            int compressedTemporalData = compressedMessage.temporalData;

            return new NetworkMovementMessage
            {
                timestamp = TimestampEncoder.Decompress(compressedTemporalData),
                position = worldPosition,
                velocity = velocity,
                movementKind = (MovementKind)((compressedTemporalData >> MOVEMENT_KIND_START_BIT) & MOVEMENT_KIND_MASK),

                animState = new AnimationStates
                {
                    MovementBlendValue = 0f,
                    SlideBlendValue = 0f,

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

    public static class ParcelEncoder
    {
        public const int PARCEL_SIZE = 16;

        // TODO (Vit): now hardcoded, but it should depend on the Genesis Size + Landscape margins settings
        public const int MIN_X = -152;
        public const int MAX_X = 164;
        public const int MIN_Y = -152;
        public const int MAX_Y = 160;

        private const int WIDTH = MAX_X - MIN_X + 1;

        public static int EncodeParcel(Vector2Int parcel) =>
            parcel.x - MIN_X + ((parcel.y - MIN_Y) * WIDTH);

        public static Vector2Int DecodeParcel(int index) =>
            new ((index % WIDTH) + MIN_X, (index / WIDTH) + MIN_Y);
    }

    public static class TimestampEncoder
    {
        public const float BUFFER = (1 << TIMESTAMP_BITS) * TIMESTAMP_QUANTUM; // maxSteps * quantum

        public static int Compress(float timestamp)
        {
            float normalizedTimestamp = timestamp % BUFFER; // Normalize timestamp within the circular buffer
            return Mathf.RoundToInt(normalizedTimestamp / TIMESTAMP_QUANTUM);
        }

        public static float Decompress(long data) =>
            data * TIMESTAMP_QUANTUM;
    }

    public static class FloatQuantizer
    {
        public static int Compress(float value, float minValue, float maxValue, int bits)
        {
            int maxStep = (1 << bits) - 1;
            float normalizedValue = (value - minValue) / (maxValue - minValue);
            return Mathf.RoundToInt(Mathf.Clamp01(normalizedValue) * maxStep);
        }

        public static float Decompress(int compressed, float minValue, float maxValue, int bits)
        {
            float maxStep = (1 << bits) - 1f;
            float normalizedValue = compressed / maxStep;
            return (normalizedValue * (maxValue - minValue)) + minValue;
        }
    }
}
