﻿using System;

namespace DCL.CharacterMotion.Components
{
    public struct CharacterAnimationComponent
    {
        public bool IsSliding;
        public AnimationStates States;
    }

    // buffer values for animation states
    [Serializable]
    public struct AnimationStates : IEquatable<AnimationStates>
    {
        private const float EPSILON = 0.001f;

        public float MovementBlendValue;
        public float SlideBlendValue;
        public bool IsGrounded;
        public bool IsJumping;
        public bool IsLongJump;
        public bool IsLongFall;
        public bool IsFalling;

        public override bool Equals(object obj) =>
            obj is AnimationStates states && Equals(states);

        public bool Equals(AnimationStates other) =>
            Math.Abs(MovementBlendValue - other.MovementBlendValue) < EPSILON &&
            Math.Abs(SlideBlendValue - other.SlideBlendValue) < EPSILON &&
            IsGrounded == other.IsGrounded &&
            IsJumping == other.IsJumping &&
            IsLongJump == other.IsLongJump &&
            IsLongFall == other.IsLongFall &&
            IsFalling == other.IsFalling;

        public override int GetHashCode()
        {
            unchecked // Overflow is fine, just wrap
            {
                var hash = 17;
                hash = (hash * 23) + MovementBlendValue.GetHashCode();
                hash = (hash * 23) + SlideBlendValue.GetHashCode();
                hash = (hash * 23) + IsGrounded.GetHashCode();
                hash = (hash * 23) + IsJumping.GetHashCode();
                hash = (hash * 23) + IsLongJump.GetHashCode();
                hash = (hash * 23) + IsLongFall.GetHashCode();
                hash = (hash * 23) + IsFalling.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            return $"{IsGrounded}: j:{IsJumping} lf:{IsLongJump} f:{IsFalling} lf:{IsLongFall} mb:{MovementBlendValue} sb:{SlideBlendValue}";
        }

        public static bool operator ==(AnimationStates left, AnimationStates right) =>
            left.Equals(right);

        public static bool operator !=(AnimationStates left, AnimationStates right) =>
            !(left == right);
    }
}
