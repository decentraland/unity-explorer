using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character.CharacterMotion.Components;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using System.Runtime.CompilerServices;

namespace DCL.CharacterMotion.Animation
{
    public static class AnimationStatesLogic
    {
        // General Animation Controller flags update
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Execute(
            ref CharacterAnimationComponent animationComponent,
            in ICharacterControllerSettings settings,
            in CharacterRigidTransform rigidTransform,
            in IAvatarView view,
            in StunComponent stunComponent)
        {
            bool isGrounded = rigidTransform is { IsGrounded: true, IsOnASteepSlope: false } || rigidTransform.IsStuck;
            float verticalVelocity = rigidTransform.GravityVelocity.y + rigidTransform.MoveVelocity.Velocity.y;

            animationComponent.States.IsGrounded = isGrounded;
            animationComponent.States.IsFalling = !isGrounded && verticalVelocity < settings.AnimationFallSpeed;
            animationComponent.States.IsLongFall = !isGrounded && verticalVelocity < settings.AnimationLongFallSpeed;
            animationComponent.States.IsLongJump = verticalVelocity > settings.RunJumpHeight * settings.RunJumpHeight * settings.JumpGravityFactor;

            bool jumpStateChanged = rigidTransform.JustJumped != animationComponent.States.IsJumping;
            SetAnimatorParameters(view, ref animationComponent.States, rigidTransform.JustJumped, jumpStateChanged, stunComponent.IsStunned);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetAnimatorParameters(IAvatarView view, ref AnimationStates states, bool isJumping, bool jumpTriggered, bool isStunned)
        {
            if (jumpTriggered)
                view.SetAnimatorTrigger(AnimationHashes.JUMP);

            states.IsJumping = isJumping;

            view.SetAnimatorBool(AnimationHashes.STUNNED, isStunned);
            view.SetAnimatorBool(AnimationHashes.GROUNDED, states.IsGrounded);
            view.SetAnimatorBool(AnimationHashes.JUMPING, states.IsJumping);
            view.SetAnimatorBool(AnimationHashes.FALLING, states.IsFalling);
            view.SetAnimatorBool(AnimationHashes.LONG_JUMP, states.IsLongJump);
            view.SetAnimatorBool(AnimationHashes.LONG_FALL, states.IsLongFall);
        }
    }
}
