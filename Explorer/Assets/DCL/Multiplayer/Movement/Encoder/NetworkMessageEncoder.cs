using DCL.CharacterMotion.Components;
using DCL.Multiplayer.Movement.Settings;
using UnityEngine;
using Utility;

namespace DCL.Multiplayer.Movement
{
    public class NetworkMessageEncoder
    {
        private readonly MessageEncodingSettings encodingSettings;

        public NetworkMessageEncoder(MessageEncodingSettings encodingSettings)
        {
            this.encodingSettings = encodingSettings;
        }

        public CompressedNetworkMovementMessage Compress(NetworkMovementMessage message) =>
            new ()
            {
                temporalData = CompressTemporalData(message.timestamp, message.movementKind, message.isSliding, message.animState, message.isStunned, message.rotationY, message.tier),
                movementData = CompressMovementData(message.position, message.velocity, encodingSettings.GetConfigForTier(message.tier)),
                original = message,
            };

        private int CompressTemporalData(float timestamp, MovementKind movementKind, bool isSliding, AnimationStates animState, bool isStunned,
            float rotationY, int tier)
        {
            int temporalData = new TimestampEncoder(encodingSettings).Compress(timestamp);

            // Animations
            temporalData |= ((int)movementKind & MessageEncodingSettings.MOVEMENT_KIND_MASK) << encodingSettings.MOVEMENT_KIND_START_BIT;
            if (isSliding) temporalData |= 1 << encodingSettings.SLIDING_BIT;
            if (isStunned) temporalData |= 1 << encodingSettings.STUNNED_BIT;
            if (animState.IsGrounded) temporalData |= 1 << encodingSettings.GROUNDED_BIT;
            if (animState.IsJumping) temporalData |= 1 << encodingSettings.JUMPING_BIT;
            if (animState.IsLongJump) temporalData |= 1 << encodingSettings.LONG_JUMP_BIT;
            if (animState.IsFalling) temporalData |= 1 << encodingSettings.FALLING_BIT;
            if (animState.IsLongFall) temporalData |= 1 << encodingSettings.LONG_FALL_BIT;

            int compressedRotation = FloatQuantizer.Compress(rotationY, 0f, 360f, encodingSettings.ROTATION_Y_BITS);
            temporalData |= compressedRotation << encodingSettings.ROTATION_START_BIT;

            temporalData |= (tier & 0x3) << encodingSettings.TIER_START_BIT;
            Debug.Log($"VVV compressed {timestamp} {tier}");

            return temporalData;
        }

        private long CompressMovementData(Vector3 position, Vector3 velocity, MovementEncodingConfig settings)
        {
            Vector2Int parcel = position.ToParcel();

            int parcelIndex = ParcelEncoder.Encode(parcel);

            var relativePosition = new Vector2(
                position.x - (parcel.x * ParcelMathHelper.PARCEL_SIZE),
                position.z - (parcel.y * ParcelMathHelper.PARCEL_SIZE) // Y is Z in this case
            );

            int xzBits = settings.XZ_BITS;
            int yMax = settings.Y_MAX;
            int yBits = settings.Y_BITS;
            int maxVelocity = settings.MAX_VELOCITY;
            int velocityBits = settings.VELOCITY_BITS;

            int compressedX = FloatQuantizer.Compress(relativePosition.x, 0, ParcelMathHelper.PARCEL_SIZE, xzBits);
            int compressedZ = FloatQuantizer.Compress(relativePosition.y, 0, ParcelMathHelper.PARCEL_SIZE, xzBits);
            int compressedY = FloatQuantizer.Compress(position.y, 0, yMax, yBits);

            int compressedVelocityX = FloatQuantizer.Compress(velocity.x, -maxVelocity, maxVelocity, velocityBits);
            int compressedVelocityY = FloatQuantizer.Compress(velocity.y, -maxVelocity, maxVelocity, velocityBits);
            int compressedVelocityZ = FloatQuantizer.Compress(velocity.z, -maxVelocity, maxVelocity, velocityBits);

            return (uint)parcelIndex
                   | ((long)compressedX << MessageEncodingSettings.PARCEL_BITS)
                   | ((long)compressedZ << (MessageEncodingSettings.PARCEL_BITS + xzBits))
                   | ((long)compressedY << (MessageEncodingSettings.PARCEL_BITS + xzBits + xzBits))
                   | ((long)compressedVelocityX << (MessageEncodingSettings.PARCEL_BITS + xzBits + xzBits + yBits))
                   | ((long)compressedVelocityY << (MessageEncodingSettings.PARCEL_BITS + xzBits + xzBits + yBits + velocityBits))
                   | ((long)compressedVelocityZ << (MessageEncodingSettings.PARCEL_BITS + xzBits + xzBits + yBits + velocityBits + velocityBits));
        }

        public NetworkMovementMessage Decompress(CompressedNetworkMovementMessage compressedMessage)
        {
            int compressedTemporalData = compressedMessage.temporalData;
            int tier = (compressedMessage.temporalData >> encodingSettings.TIER_START_BIT) & 0x3;

            (Vector3 position, Vector3 velocity) movementData = DecompressMovementData(compressedMessage.movementData, encodingSettings.GetConfigForTier(tier));

            int rotationMask = (1 << encodingSettings.ROTATION_Y_BITS) - 1;
            int compressedRotation = (compressedTemporalData >> encodingSettings.ROTATION_START_BIT) & rotationMask;
            float timestamp = new TimestampEncoder(encodingSettings).Decompress(compressedTemporalData);
            Debug.Log($"VVV decompressed {timestamp} {tier}");

            return new NetworkMovementMessage
            {
                tier = tier,
                // Decompressed movement data
                position = encodingSettings.encodePosition? movementData.position : compressedMessage.original.position,
                velocity = encodingSettings.encodeVelocity? movementData.velocity : compressedMessage.original.velocity,
                rotationY = FloatQuantizer.Decompress(compressedRotation, 0f, 360f, encodingSettings.ROTATION_Y_BITS),

                // Decompress temporal data
                timestamp = encodingSettings.encodeTimestamp? timestamp : compressedMessage.original.timestamp,
                movementKind = (MovementKind)((compressedTemporalData >> encodingSettings.MOVEMENT_KIND_START_BIT) & MessageEncodingSettings.MOVEMENT_KIND_MASK),

                animState = new AnimationStates
                {
                    MovementBlendValue = 0f,
                    SlideBlendValue = 0f,

                    IsGrounded = (compressedTemporalData & (1 << encodingSettings.GROUNDED_BIT)) != 0,
                    IsJumping = (compressedTemporalData & (1 << encodingSettings.JUMPING_BIT)) != 0,
                    IsLongJump = (compressedTemporalData & (1 << encodingSettings.LONG_JUMP_BIT)) != 0,
                    IsFalling = (compressedTemporalData & (1 << encodingSettings.FALLING_BIT)) != 0,
                    IsLongFall = (compressedTemporalData & (1 << encodingSettings.LONG_FALL_BIT)) != 0,
                },

                isStunned = (compressedTemporalData & (1 << encodingSettings.STUNNED_BIT)) != 0,
                isSliding = (compressedTemporalData & (1 << encodingSettings.SLIDING_BIT)) != 0,
            };
        }

        private (Vector3 position, Vector3 velocity) DecompressMovementData(long movementData, MovementEncodingConfig settings)
        {
            const int PARCEL_BITS = MessageEncodingSettings.PARCEL_BITS;
            const int PARCEL_MASK = (1 << PARCEL_BITS) - 1;

            int xzBits = settings.XZ_BITS;
            int xzMask = (1 << xzBits) - 1;

            int yBits = settings.Y_BITS;
            int yMax = settings.Y_MAX;
            int yMask = (1 << yBits) - 1;

            int maxVelocity = settings.MAX_VELOCITY;
            int velocityBits = settings.VELOCITY_BITS;
            int velocityMask = (1 << velocityBits) - 1;

            Vector2Int parcel = ParcelEncoder.Decode((int)(movementData & PARCEL_MASK));

            var extractedX = (int)((movementData >> PARCEL_BITS) & xzMask);
            var extractedZ = (int)((movementData >> (PARCEL_BITS + xzBits)) & xzMask);
            var extractedY = (int)((movementData >> (PARCEL_BITS + xzBits + xzBits)) & yMask);

            float decompressedX = FloatQuantizer.Decompress(extractedX, 0, ParcelMathHelper.PARCEL_SIZE, xzBits);
            float decompressedZ = FloatQuantizer.Decompress(extractedZ, 0, ParcelMathHelper.PARCEL_SIZE, xzBits);
            float decompressedY = FloatQuantizer.Decompress(extractedY, 0, yMax, yBits);

            var worldPosition = new Vector3(
                (parcel.x * ParcelMathHelper.PARCEL_SIZE) + decompressedX,
                decompressedY,
                (parcel.y * ParcelMathHelper.PARCEL_SIZE) + decompressedZ
            );

            var extractedVelocityX = (int)((movementData >> (PARCEL_BITS + xzBits + xzBits + yBits)) & velocityMask);
            var extractedVelocityY = (int)((movementData >> (PARCEL_BITS + xzBits + xzBits + yBits + velocityBits)) & velocityMask);
            var extractedVelocityZ = (int)((movementData >> (PARCEL_BITS + xzBits + xzBits + yBits + velocityBits + velocityBits)) & velocityMask);

            float decompressedVelocityX = FloatQuantizer.Decompress(extractedVelocityX, -maxVelocity, maxVelocity, velocityBits);
            float decompressedVelocityY = FloatQuantizer.Decompress(extractedVelocityY, -maxVelocity, maxVelocity, velocityBits);
            float decompressedVelocityZ = FloatQuantizer.Decompress(extractedVelocityZ, -maxVelocity, maxVelocity, velocityBits);

            return (worldPosition, velocity: new Vector3(decompressedVelocityX, decompressedVelocityY, decompressedVelocityZ));
        }
    }

    /// <summary>
    ///     Flatten (x,y) parcel coordinates into 1-dimensional array
    /// </summary>
    public static class ParcelEncoder
    {
        // TODO (Vit): now hardcoded, but it should depend on the Genesis Size + Landscape margins settings
        public const int MIN_X = -152;
        public const int MAX_X = 164;
        public const int MIN_Y = -152;
        public const int MAX_Y = 160;

        private const int WIDTH = MAX_X - MIN_X + 1;

        public static int Encode(Vector2Int parcel) =>
            parcel.x - MIN_X + ((parcel.y - MIN_Y) * WIDTH);

        public static Vector2Int Decode(int index) =>
            new ((index % WIDTH) + MIN_X, (index / WIDTH) + MIN_Y);
    }

    /// <summary>
    ///     Encode timepsamp as a circular buffer
    /// </summary>
    public class TimestampEncoder
    {
        private readonly MessageEncodingSettings settings;
        public float BUFFER => STEPS * settings.TIMESTAMP_QUANTUM;
        private int STEPS => 1 << settings.TIMESTAMP_BITS; // 2^TIMESTAMP_BITS

        public TimestampEncoder(MessageEncodingSettings settings)
        {
            this.settings = settings;
        }

        public int Compress(float timestamp)
        {
            float normalizedTimestamp = timestamp % BUFFER; // Normalize timestamp within the round buffer
            return Mathf.RoundToInt(normalizedTimestamp / settings.TIMESTAMP_QUANTUM) % STEPS;
        }

        public float Decompress(long data)
        {
            int MASK = STEPS - 1;
            return (int)(data & MASK) * settings.TIMESTAMP_QUANTUM % BUFFER;
        }
    }

    /// <summary>
    ///     Compress float via scaled integer approach (fixed-size quantization)
    /// </summary>
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
