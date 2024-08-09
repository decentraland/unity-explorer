using DCL.CharacterMotion.Components;
using System;
using UnityEngine;

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
        public NetworkMovementMessage message;
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
            float normalizedTimestamp = message.timestamp % ROUND_BUFFER; // Normalize timestamp within the round buffer
            long compressedTimestamp = Mathf.RoundToInt(normalizedTimestamp / SENT_INTERVAL) % steps;

            return new CompressedNetworkMovementMessage
            {
                AnimState = message.ToProtoEnum(),
                compressedData = compressedTimestamp,
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
            int mask = steps == STEPS9_BIT ? 0x1FF :
                steps == STEPS8_BIT ? 0xFF : 0x7F;

            var compressedTimestamp = (int)(compressedMessage.compressedData & mask);

            // Debug.Assert(quantum == RoundBufferSeconds / steps, "VVV should be equal");
            float timestamp = compressedTimestamp * SENT_INTERVAL % ROUND_BUFFER;

            AnimationStates animState = compressedMessage.AnimState.ToAnimState();
            animState.MovementBlendValue = compressedMessage.message.animState.MovementBlendValue;
            animState.SlideBlendValue = compressedMessage.message.animState.SlideBlendValue;
            animState.IsJumping = compressedMessage.message.animState.IsJumping;
            animState.IsLongJump = compressedMessage.message.animState.IsLongJump;

            animState = compressedMessage.message.animState;

            return new NetworkMovementMessage
            {
                timestamp = timestamp,
                position = compressedMessage.message.position,
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
