using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using System.Runtime.CompilerServices;
using UnityEngine;
using Utility.Animations;

namespace DCL.CharacterMotion.Animation
{
    public static class AnimationStatesLogic
    {
        // General Animation Controller flags update
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Execute(float dt,
            ICharacterControllerSettings settings,
            ref CharacterAnimationComponent animationComponent,
            IAvatarView view,
            CharacterRigidTransform rigidTransform,
            in StunComponent stunComponent,
            in JumpState jumpState,
            in GlideState glideState)
        {
            bool isGrounded = rigidTransform is { IsGrounded: true, IsOnASteepSlope: false } || rigidTransform.IsStuck;

            Vector3 velocity = rigidTransform.MoveVelocity.Velocity;
            float verticalVelocity = rigidTransform.GravityVelocity.y + velocity.y;

            bool jumpTriggered = jumpState.JumpCount > animationComponent.States.JumpCount;
            bool glidingTriggered = glideState.Value == GlideStateValue.OPENING_PROP && animationComponent.States.GlideState != GlideStateValue.OPENING_PROP;

            animationComponent.States.IsGrounded = isGrounded;
            animationComponent.States.JumpCount = jumpState.JumpCount;
            animationComponent.States.IsLongJump = verticalVelocity > settings.RunJumpHeight * settings.RunJumpHeight * settings.JumpGravityFactor;
            animationComponent.States.IsFalling = !isGrounded && verticalVelocity < settings.AnimationFallSpeed;
            animationComponent.States.IsLongFall = !isGrounded && verticalVelocity < settings.AnimationLongFallSpeed;
            animationComponent.States.IsStunned = stunComponent.IsStunned;
            animationComponent.States.GlideState = glideState.Value;
            animationComponent.States.GlideBlendValue = UpdateGlideBlendValue(dt, settings, animationComponent.States.GlideBlendValue, view, velocity);

            SetAnimatorParameters(view, animationComponent.States, jumpTriggered, glidingTriggered);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float UpdateGlideBlendValue(float dt, ICharacterControllerSettings settings, float glideBlend, IAvatarView view, Vector3 velocity)
        {
            float maxAngle = Mathf.Deg2Rad * (90 - settings.GlideAnimMaxAngle);

            const float BLEND_MIN_VEL_SQ = 0.01f * 0.01f;
            float cos = velocity.sqrMagnitude > BLEND_MIN_VEL_SQ ? Vector3.Dot(velocity.normalized, view.GetTransform().right) : 0;
            cos = Mathf.Clamp(cos / Mathf.Cos(maxAngle), -1, 1);

            float alpha = 1 - Mathf.Exp(-settings.GlideAnimBlendSpeed * dt);
            return Mathf.Lerp(glideBlend, cos, alpha);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetAnimatorParameters(IAvatarView view, in AnimationStates states, bool jumpTriggered, bool glidingTriggered)
        {
            view.SetAnimatorBool(AnimationHashes.GROUNDED, states.IsGrounded);
            view.SetAnimatorInt(AnimationHashes.JUMP_COUNT, states.JumpCount);
            view.SetAnimatorBool(AnimationHashes.LONG_JUMP, states.IsLongJump);
            view.SetAnimatorBool(AnimationHashes.FALLING, states.IsFalling);
            view.SetAnimatorBool(AnimationHashes.LONG_FALL, states.IsLongFall);
            view.SetAnimatorBool(AnimationHashes.STUNNED, states.IsStunned);
            view.SetAnimatorBool(AnimationHashes.GLIDING, states.GlideState is GlideStateValue.OPENING_PROP or GlideStateValue.GLIDING);
            view.SetAnimatorFloat(AnimationHashes.GLIDE_BLEND, states.GlideBlendValue);

            if (jumpTriggered) view.SetAnimatorTrigger(AnimationHashes.JUMP);
            if (glidingTriggered) view.SetAnimatorTrigger(AnimationHashes.START_GLIDING);
        }
    }
}
