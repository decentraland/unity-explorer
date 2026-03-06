using UnityEngine;

namespace Utility.Animations
{
    public static class AnimationHashes
    {
        public static readonly int ENABLE = Animator.StringToHash("Enable");
        public static readonly int EMOTE = Animator.StringToHash("Emote");
        public static readonly int EMOTE_LOOP = Animator.StringToHash("EmoteLoop");
        public static readonly int LOOP = Animator.StringToHash("Loop");
        public static readonly int EMOTE_RESET = Animator.StringToHash("EmoteForceRestart");
        public static readonly int EMOTE_STOP = Animator.StringToHash("EmoteStop");
        public static readonly int MOVEMENT_BLEND = Animator.StringToHash("MovementBlend");
        public static readonly int MOVEMENT_TYPE = Animator.StringToHash("MovementType"); //We use this for sounds playback, to know exactly which expected movement type we are doing independently from blend value
        public static readonly int GROUNDED = Animator.StringToHash("IsGrounded");
        public static readonly int FALLING = Animator.StringToHash("IsFalling");
        public static readonly int LONG_JUMP = Animator.StringToHash("IsLongJump");
        public static readonly int JUMP = Animator.StringToHash("Jump");
        public static readonly int JUMP_COUNT = Animator.StringToHash("JumpCount");
        public static readonly int JUMPING_TAG = Animator.StringToHash("Jumping");
        public static readonly int LONG_FALL = Animator.StringToHash("IsLongFall");
        public static readonly int STUNNED = Animator.StringToHash("IsStunned");
        public static readonly int SLIDE_BLEND = Animator.StringToHash("SlideBlend");
        public static readonly int START_GLIDING = Animator.StringToHash("StartGliding");
        public static readonly int GLIDING = Animator.StringToHash("IsGliding");
        public static readonly int GLIDE_BLEND = Animator.StringToHash("GlideBlend");
        public static readonly int JUMP_VARIATION = Animator.StringToHash("JumpVariation");
    }
}
