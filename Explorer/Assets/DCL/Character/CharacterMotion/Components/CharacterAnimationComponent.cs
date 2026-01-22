using System;

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
        public int JumpCount;
        public bool IsLongJump;
        public bool IsFalling;
        public bool IsLongFall;
        public bool IsStunned;
        public bool IsGliding;
        public float GlideBlendValue;

        public override bool Equals(object obj) =>
            obj is AnimationStates states && Equals(states);

        public bool Equals(AnimationStates other) =>
            Math.Abs(MovementBlendValue - other.MovementBlendValue) < EPSILON &&
            Math.Abs(SlideBlendValue - other.SlideBlendValue) < EPSILON &&
            IsGrounded == other.IsGrounded &&
            JumpCount == other.JumpCount &&
            IsLongJump == other.IsLongJump &&
            IsFalling == other.IsFalling &&
            IsLongFall == other.IsLongFall &&
            IsLongFall == other.IsStunned &&
            IsGliding == other.IsGliding &&
            Math.Abs(GlideBlendValue - other.GlideBlendValue) < EPSILON;

        public override int GetHashCode()
        {
            unchecked // Overflow is fine, just wrap
            {
                var hash = 17;
                hash = (hash * 23) + MovementBlendValue.GetHashCode();
                hash = (hash * 23) + SlideBlendValue.GetHashCode();
                hash = (hash * 23) + IsGrounded.GetHashCode();
                hash = (hash * 23) + JumpCount;
                hash = (hash * 23) + IsLongJump.GetHashCode();
                hash = (hash * 23) + IsFalling.GetHashCode();
                hash = (hash * 23) + IsLongFall.GetHashCode();
                hash = (hash * 23) + IsStunned.GetHashCode();
                hash = (hash * 23) + IsGliding.GetHashCode();
                hash = (hash * 23) + GlideBlendValue.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            return $"gr:{IsGrounded}: jc:{JumpCount} lj:{IsLongJump} f:{IsFalling} lf:{IsLongFall} gl:{IsGliding} mb:{MovementBlendValue} sb:{SlideBlendValue}";
        }

        public static bool operator ==(AnimationStates left, AnimationStates right) =>
            left.Equals(right);

        public static bool operator !=(AnimationStates left, AnimationStates right) =>
            !(left == right);
    }
}
