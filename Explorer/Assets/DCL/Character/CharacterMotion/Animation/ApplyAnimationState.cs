using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using System.Runtime.CompilerServices;

namespace DCL.CharacterMotion.Animation
{
    public static class ApplyAnimationState
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
            bool isGrounded = (rigidTransform.IsGrounded && !rigidTransform.IsOnASteepSlope) || rigidTransform.IsStuck;
            float verticalVelocity = rigidTransform.GravityVelocity.y + rigidTransform.MoveVelocity.Velocity.y;

            animationComponent.States.IsGrounded = isGrounded;
            animationComponent.States.IsFalling = !isGrounded && verticalVelocity < settings.AnimationFallSpeed;
            animationComponent.States.IsLongFall = !isGrounded && verticalVelocity < settings.AnimationLongFallSpeed;
            animationComponent.States.IsLongJump = verticalVelocity > settings.RunJumpHeight * settings.RunJumpHeight * settings.JumpGravityFactor;

            if (rigidTransform.JustJumped && !animationComponent.States.IsJumping)
                view.SetAnimatorTrigger(AnimationHashes.JUMP);

            animationComponent.States.IsJumping = rigidTransform.JustJumped;

            view.SetAnimatorBool(AnimationHashes.STUNNED, stunComponent.IsStunned);
            view.SetAnimatorBool(AnimationHashes.GROUNDED, animationComponent.States.IsGrounded);
            view.SetAnimatorBool(AnimationHashes.JUMPING, animationComponent.States.IsJumping);
            view.SetAnimatorBool(AnimationHashes.FALLING, animationComponent.States.IsFalling);
            view.SetAnimatorBool(AnimationHashes.LONG_JUMP, animationComponent.States.IsLongJump);
            view.SetAnimatorBool(AnimationHashes.LONG_FALL, animationComponent.States.IsLongFall);

            ExecuteEmote(ref animationComponent, in view);
        }


        // General Animation Controller flags update
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ExecuteEmote(ref CharacterAnimationComponent animationComponent, in IAvatarView view)
        {
            // If the avatar is already doing an emote and we re-trigger it, we want to restart the animation to enable emote spamming
            if (animationComponent.States.WasEmoteJustTriggered)
            {
                if (view.IsAnimatorInTag("Emote"))
                    view.SetAnimatorTrigger(AnimationHashes.EMOTE_RESET);
                else
                    view.SetAnimatorTrigger(AnimationHashes.EMOTE);

                view.SetAnimatorBool(AnimationHashes.EMOTE_LOOP, animationComponent.States.EmoteLoop);
                view.ReplaceEmoteAnimation(animationComponent.States.EmoteClip);
            }

            animationComponent.States.WasEmoteJustTriggered = false;
        }
    }
}
