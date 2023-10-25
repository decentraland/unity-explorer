using DCL.AvatarRendering.AvatarShape;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using System.Runtime.CompilerServices;

namespace DCL.CharacterMotion.Animation
{
    public static class ApplyJumpState
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Execute(ref CharacterAnimationComponent animationComponent,
            in ICharacterControllerSettings settings,
            in CharacterRigidTransform rigidTransform,
            in AvatarBase avatarBase,
            in StunComponent stunComponent)
        {
            var animator = avatarBase.avatarAnimator;

            animationComponent.States.IsGrounded = rigidTransform.IsGrounded;
            animationComponent.States.IsFalling = !rigidTransform.IsGrounded && rigidTransform.NonInterpolatedVelocity.y < -5f;
            animationComponent.States.IsLongFall = !rigidTransform.IsGrounded && rigidTransform.NonInterpolatedVelocity.y < -12f;
            animationComponent.States.IsLongJump = rigidTransform.NonInterpolatedVelocity.y > settings.JogJumpHeight * 3 * settings.JumpGravityFactor;

            bool jumpState = !rigidTransform.IsGrounded && (rigidTransform.NonInterpolatedVelocity.y > 5f || animationComponent.States.IsLongJump);

            if (jumpState && !animationComponent.States.IsJumping)
                animator.SetTrigger(AnimationHashes.JUMP);

            animationComponent.States.IsJumping = jumpState;

            if (stunComponent.IsStunned && !animationComponent.States.IsStunned)
                animator.SetTrigger(AnimationHashes.STUNNED);

            animationComponent.States.IsStunned = stunComponent.IsStunned;

            animator.SetBool(AnimationHashes.GROUNDED, animationComponent.States.IsGrounded);
            animator.SetBool(AnimationHashes.JUMPING, animationComponent.States.IsJumping);
            animator.SetBool(AnimationHashes.FALLING, animationComponent.States.IsFalling);
            animator.SetBool(AnimationHashes.LONG_JUMP, animationComponent.States.IsLongJump);
            animator.SetBool(AnimationHashes.LONG_FALL, animationComponent.States.IsLongFall);
        }
    }
}
