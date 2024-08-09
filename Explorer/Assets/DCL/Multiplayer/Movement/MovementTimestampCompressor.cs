using DCL.CharacterMotion.Components;
using System;
using UnityEngine;
using Utility;

namespace DCL.Multiplayer.Movement.Systems
{
    public enum NetworkAnimState
    {
        isNotMoving,

        isGrounded,
        isStunned, // isStunned enables isGrounded

        isJumping,
        isLongJump, // isLongJump enables isJumping

        isFalling,
        isLongFall, // isLongFall enables isFalling
    }

    [Serializable]
    public struct CompressedNetworkMovementMessage
    {
        public NetworkAnimState AnimState;
        public long compressedData;
        public Vector3 remainedPosition;

        public NetworkMovementMessage message;
    }

    public static class ParcelEncoder
    {
        public const int PARCEL_SIZE = 16;

        // TODO (Vit): now hardcoded, but it should depend on the Landscape margins and Genesis Parcel data
        private const int MIN_X = -152;
        private const int MAX_X = 164;

        private const int MIN_Y = -152;
        private const int MAX_Y = 160;

        private const int WIDTH = MAX_X - MIN_X + 1;

        public static int EncodeParcel(Vector2Int parcel) =>
            (parcel.x - MIN_X) + ((parcel.y - MIN_Y) * WIDTH);

        public static Vector2Int DecodeParcel(int index) =>
            new ((index % WIDTH) + MIN_X, (index / WIDTH) + MIN_Y);
    }

    public static class NetworkMessageCompressor
    {
        // private const float ROUND_BUFFER_SECONDS = 51.2f;
        public const int STEPS7_BIT = 128;
        public const int STEPS8_BIT = 256;
        public const int STEPS9_BIT = 512;

        public const float SENT_INTERVAL = 0.1f; // == QUANTUM in this case

        public const float ROUND_BUFFER = STEPS8_BIT * SENT_INTERVAL;

        public static CompressedNetworkMovementMessage Compress(this NetworkMovementMessage message, int steps = STEPS8_BIT)
        {
            // Compress timestamp (8 bits)
            float normalizedTimestamp = message.timestamp % ROUND_BUFFER; // Normalize timestamp within the round buffer
            long compressedTimestamp = Mathf.RoundToInt(normalizedTimestamp / SENT_INTERVAL) % steps;

            // Encode parcel (17 bits)
            var parcel = message.position.ToParcel();
            int parcelIndex = ParcelEncoder.EncodeParcel(parcel);

            // Combine timestamp and parcel index
            long compressedData = compressedTimestamp | ((long)parcelIndex << 8);

            Vector2 relativePosition = new Vector2(
                message.position.x - (parcel.x * ParcelEncoder.PARCEL_SIZE),
                message.position.z - (parcel.y * ParcelEncoder.PARCEL_SIZE)
            );

            return new CompressedNetworkMovementMessage
            {
                AnimState = message.ToProtoEnum(),
                compressedData = compressedData,
                remainedPosition = new Vector3(relativePosition.x, message.position.y, relativePosition.y),
                message = message,
            };
        }

        private static NetworkAnimState ToProtoEnum(this NetworkMovementMessage message)
        {
            if (message.isStunned)
                return NetworkAnimState.isStunned;

            if (message.animState.IsGrounded)
                return NetworkAnimState.isGrounded;

            if (message.animState.IsLongFall)
                return NetworkAnimState.isLongFall;

            if (message.animState.IsFalling)
                return NetworkAnimState.isFalling;

            if (message.animState.IsLongJump)
                return NetworkAnimState.isLongJump;

            if (message.animState.IsJumping)
                return NetworkAnimState.isJumping;

            return NetworkAnimState.isNotMoving;
        }

        public static NetworkMovementMessage Decompress(this CompressedNetworkMovementMessage compressedMessage, int steps = STEPS8_BIT)
        {
            int mask = steps switch
                       {
                           STEPS9_BIT => 0x1FF,
                           STEPS8_BIT => 0xFF,
                           _ => 0x7F, // 7 BITS
                       };

            var compressedTimestamp = (int)(compressedMessage.compressedData & mask);
            // Debug.Assert(quantum == RoundBufferSeconds / steps, "VVV should be equal");
            float timestamp = compressedTimestamp * SENT_INTERVAL % ROUND_BUFFER;

            // Extract parcel index (17 bits)
            int parcelIndex = (int)((compressedMessage.compressedData >> 8) & 0x1FFFF);
            var parcel = ParcelEncoder.DecodeParcel(parcelIndex);
            Vector3 worldPosition = new Vector3(
                                        (parcel.x * ParcelEncoder.PARCEL_SIZE) + compressedMessage.remainedPosition.x,
                                        compressedMessage.remainedPosition.y,
                                        (parcel.y * ParcelEncoder.PARCEL_SIZE) + compressedMessage.remainedPosition.z);

            AnimationStates animState = compressedMessage.AnimState.ToAnimState();
            {
                animState.MovementBlendValue = compressedMessage.message.animState.MovementBlendValue;
                animState.SlideBlendValue = compressedMessage.message.animState.SlideBlendValue;
                animState.IsJumping = compressedMessage.message.animState.IsJumping;
                animState.IsLongJump = compressedMessage.message.animState.IsLongJump;

                animState = compressedMessage.message.animState;
            }

            return new NetworkMovementMessage
            {
                timestamp = timestamp,
                position = worldPosition,
                velocity = compressedMessage.message.velocity,
                animState = animState,
                isStunned = compressedMessage.message.isStunned,
            };
        }

        private static AnimationStates ToAnimState(this NetworkAnimState protoAnimEnum)
        {
            var animStates = new AnimationStates();

            switch (protoAnimEnum)
            {
                default:
                case NetworkAnimState.isNotMoving:
                case NetworkAnimState.isGrounded:
                case NetworkAnimState.isStunned:
                    animStates.IsGrounded = true;
                    animStates.IsLongJump = animStates.IsJumping = animStates.IsLongFall = animStates.IsFalling = false;
                    break;
                case NetworkAnimState.isLongFall:
                    animStates.IsLongFall = animStates.IsFalling = true;
                    animStates.IsGrounded = animStates.IsJumping = animStates.IsLongJump = false;
                    break;
                case NetworkAnimState.isFalling:
                    animStates.IsFalling = true;
                    animStates.IsLongFall = false;
                    animStates.IsGrounded = animStates.IsJumping = animStates.IsLongJump = false;
                    break;
                case NetworkAnimState.isLongJump:
                    animStates.IsLongJump = animStates.IsJumping = true;
                    animStates.IsGrounded = animStates.IsFalling = animStates.IsLongFall = false;
                    break;
                case NetworkAnimState.isJumping:
                    animStates.IsJumping = true;
                    animStates.IsLongJump = false;
                    animStates.IsGrounded = animStates.IsFalling = animStates.IsLongFall = false;
                    break;
            }

            return animStates;
        }
    }
}
