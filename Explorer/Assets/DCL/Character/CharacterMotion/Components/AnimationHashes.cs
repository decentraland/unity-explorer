using UnityEngine;

namespace DCL.Character.CharacterMotion.Components
{
    public static class AnimationHashes
    {
        public static readonly int EMOTE = Animator.StringToHash("Emote");
        public static readonly int EMOTE_LOOP = Animator.StringToHash("EmoteLoop");
        public static readonly int EMOTE_RESET = Animator.StringToHash("EmoteForceRestart");
        public static readonly int MOVEMENT_BLEND = Animator.StringToHash("MovementBlend");
        public static readonly int GROUNDED = Animator.StringToHash("IsGrounded");
        public static readonly int JUMPING = Animator.StringToHash("IsJumping");
        public static readonly int FALLING = Animator.StringToHash("IsFalling");
        public static readonly int LONG_JUMP = Animator.StringToHash("IsLongJump");
        public static readonly int JUMP = Animator.StringToHash("Jump");
        public static readonly int WALL_HIT = Animator.StringToHash("WallHit");
        public static readonly int ANGLE = Animator.StringToHash("Angle");
        public static readonly int ANGLE_DIR = Animator.StringToHash("AngleDir");
        public static readonly int LONG_FALL = Animator.StringToHash("IsLongFall");
        public static readonly int STUNNED = Animator.StringToHash("IsStunned");
        public static readonly int SLIDE_BLEND = Animator.StringToHash("SlideBlend");
        public static readonly int AFK = Animator.StringToHash("AFK");
    }
}
