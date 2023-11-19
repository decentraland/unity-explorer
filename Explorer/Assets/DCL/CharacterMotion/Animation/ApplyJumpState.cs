using DCL.AvatarRendering.AvatarShape.UnityInterface;
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
            in IAvatarView view,
            in StunComponent stunComponent)
        {
            animationComponent.States.IsGrounded = rigidTransform.IsGrounded;
            animationComponent.States.IsFalling = !rigidTransform.IsGrounded && rigidTransform.NonInterpolatedVelocity.y < settings.AnimationFallSpeed;
            animationComponent.States.IsLongFall = !rigidTransform.IsGrounded && rigidTransform.NonInterpolatedVelocity.y < settings.AnimationLongFallSpeed;
            animationComponent.States.IsLongJump = rigidTransform.NonInterpolatedVelocity.y > settings.RunJumpHeight * settings.RunJumpHeight * settings.JumpGravityFactor;

            bool jumpState = !rigidTransform.IsGrounded && (rigidTransform.NonInterpolatedVelocity.y > -settings.AnimationFallSpeed || animationComponent.States.IsLongJump);

            if (jumpState && !animationComponent.States.IsJumping)
                view.SetAnimatorTrigger(AnimationHashes.JUMP);

            animationComponent.States.IsJumping = jumpState;

            if (stunComponent.IsStunned && !animationComponent.States.IsStunned)
                view.SetAnimatorTrigger(AnimationHashes.STUNNED);

            animationComponent.States.IsStunned = stunComponent.IsStunned;

            view.SetAnimatorBool(AnimationHashes.GROUNDED, animationComponent.States.IsGrounded);
            view.SetAnimatorBool(AnimationHashes.JUMPING, animationComponent.States.IsJumping);
            view.SetAnimatorBool(AnimationHashes.FALLING, animationComponent.States.IsFalling);
            view.SetAnimatorBool(AnimationHashes.LONG_JUMP, animationComponent.States.IsLongJump);
            view.SetAnimatorBool(AnimationHashes.LONG_FALL, animationComponent.States.IsLongFall);
        }
    }
}
