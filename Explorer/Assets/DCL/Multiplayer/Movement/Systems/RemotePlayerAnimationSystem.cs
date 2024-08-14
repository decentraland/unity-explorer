using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character.CharacterMotion.Components;
using DCL.Character.Components;
using DCL.CharacterMotion.Animation;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using DCL.Diagnostics;
using DCL.Multiplayer.Movement.Settings;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using UnityEngine;
using static DCL.CharacterMotion.Animation.AnimationMovementBlendLogic;

namespace DCL.Multiplayer.Movement.Systems
{
    [UpdateInGroup(typeof(RemoteMotionGroup))]
    [UpdateAfter(typeof(RemotePlayersMovementSystem))]
    [LogCategory(ReportCategory.MULTIPLAYER_MOVEMENT)]
    public partial class RemotePlayerAnimationSystem : BaseUnityLoopSystem
    {
        private const float JUMP_EPSILON = 0.01f;
        private const float MOVEMENT_EPSILON = 0.01f;

        // Found empirically and diverges a bit from the character settings (where speeds are RUN = 10, JOG = 8, WALK = 1.5)
        private const float RUN_SPEED_THRESHOLD = 9.5f;
        private const float JOG_SPEED_THRESHOLD = 4f;

        private readonly RemotePlayerExtrapolationSettings settings;
        private readonly ICharacterControllerSettings characterControllerSettings;

        public RemotePlayerAnimationSystem(World world, RemotePlayerExtrapolationSettings settings, ICharacterControllerSettings characterControllerSettings) : base(world)
        {
            this.settings = settings;
            this.characterControllerSettings = characterControllerSettings;
        }

        protected override void Update(float t)
        {
            UpdatePlayersAnimationQuery(World, t);
        }

        [Query]
        [None(typeof(PlayerComponent), typeof(DeleteEntityIntention))]
        private void UpdatePlayersAnimation([Data] float t, in IAvatarView view, ref CharacterAnimationComponent anim,
            ref RemotePlayerMovementComponent remotePlayerMovement, ref InterpolationComponent intComp, ref ExtrapolationComponent extComp)
        {
            if (remotePlayerMovement.WasPassedThisFrame)
            {
                remotePlayerMovement.WasPassedThisFrame = false;
                UpdateAnimations(t, view, ref anim, remotePlayerMovement.PastMessage);
            }

            if (intComp.Enabled)
                InterpolateAnimations(view, ref anim, intComp);
            else if (extComp.Enabled)
                ExtrapolateAnimations(view, ref anim, extComp.Time, extComp.TotalMoveDuration, settings.LinearTime);
        }

        private void UpdateAnimations(float t, IAvatarView view, ref CharacterAnimationComponent animationComponent, in NetworkMovementMessage message)
        {
            var animState = message.animState;

            if (animationComponent.States.Equals(animState))
                return;

            AnimationMovementBlendLogic.Apply(t, ref animationComponent, message.velocity, animState.IsGrounded, message.movementKind, view, characterControllerSettings);

            AnimationSlideBlendLogic.Apply(t, ref animationComponent, message.isSliding, view, characterControllerSettings);
            animationComponent.IsSliding = message.isSliding;

            bool jumpStateChanged = (animationComponent.States.IsGrounded && !animState.IsGrounded) || (!animationComponent.States.IsJumping && animState.IsJumping);
            AnimationStatesLogic.Apply(view, ref animationComponent.States, animState.IsJumping, jumpStateChanged, message.isStunned);

            animationComponent.States.IsGrounded = animState.IsGrounded;
            animationComponent.States.IsJumping = animState.IsJumping;
            animationComponent.States.IsLongJump = animState.IsLongJump;
            animationComponent.States.IsFalling = animState.IsFalling;
            animationComponent.States.IsLongFall = animState.IsLongFall;
        }

        private static void InterpolateAnimations(IAvatarView view, ref CharacterAnimationComponent anim, in InterpolationComponent intComp)
        {
            if (!anim.States.IsJumping && intComp.End.animState.IsJumping && Mathf.Abs(intComp.Start.position.y - intComp.End.position.y) > JUMP_EPSILON)
                AnimateFutureJump(view, ref anim, intComp.End.animState);

            AnimationStates startAnimStates = intComp.Start.animState;
            AnimationStates endAnimStates = intComp.End.animState;

            bool bothPointBlendsAreZero = startAnimStates.MovementBlendValue < BLEND_EPSILON && endAnimStates.MovementBlendValue < BLEND_EPSILON
                                                                                             && startAnimStates.SlideBlendValue < BLEND_EPSILON && endAnimStates.SlideBlendValue < BLEND_EPSILON;

            if (bothPointBlendsAreZero && Vector3.SqrMagnitude(intComp.Start.position - intComp.End.position) > MOVEMENT_EPSILON)
                BlendBetweenTwoZeroMovementPoints(ref anim, intComp);
            else
            {
                anim.States.MovementBlendValue = Mathf.Lerp(startAnimStates.MovementBlendValue, endAnimStates.MovementBlendValue, intComp.Time / intComp.TotalDuration);
                anim.States.SlideBlendValue = Mathf.Lerp(startAnimStates.SlideBlendValue, endAnimStates.SlideBlendValue, intComp.Time / intComp.TotalDuration);
            }

            UpdateLocalBlends(view, anim.States);
        }

        private static void AnimateFutureJump(IAvatarView view, ref CharacterAnimationComponent anim, in AnimationStates animState)
        {
            anim.States.IsGrounded = animState.IsGrounded;
            anim.States.IsJumping = animState.IsJumping;

            view.SetAnimatorTrigger(AnimationHashes.JUMP);

            view.SetAnimatorBool(AnimationHashes.GROUNDED, anim.States.IsGrounded);
            view.SetAnimatorBool(AnimationHashes.JUMPING, anim.States.IsJumping);
        }

        private static void BlendBetweenTwoZeroMovementPoints(ref CharacterAnimationComponent anim, in InterpolationComponent intComp)
        {
            float speed = Vector3.Distance(intComp.Start.position, intComp.End.position) / intComp.TotalDuration;

            // 3 - run, 2 - jog, 1 - walk.
            float midPointBlendValue = speed > RUN_SPEED_THRESHOLD ? 3 :
                speed > JOG_SPEED_THRESHOLD ? 2 : 1;

            float lerpValue = intComp.Time / intComp.TotalDuration;

            if (intComp.Time < intComp.TotalDuration / 2)
            {
                anim.States.MovementBlendValue = Mathf.Lerp(intComp.Start.animState.MovementBlendValue, midPointBlendValue, lerpValue);
                anim.States.SlideBlendValue = Mathf.Lerp(intComp.Start.animState.SlideBlendValue, midPointBlendValue, lerpValue);
            }
            else
            {
                anim.States.MovementBlendValue = Mathf.Lerp(midPointBlendValue, intComp.End.animState.MovementBlendValue, lerpValue);
                anim.States.SlideBlendValue = Mathf.Lerp(midPointBlendValue, intComp.End.animState.SlideBlendValue, lerpValue);
            }
        }

        private static void ExtrapolateAnimations(IAvatarView view, ref CharacterAnimationComponent anim, float time, float totalMoveDuration, float linearTime)
        {
            if (time >= totalMoveDuration)
            {
                anim.States.MovementBlendValue = 0f;
                anim.States.SlideBlendValue = 0f;
            }
            else if (time > linearTime && time < totalMoveDuration)
            {
                float dampDuration = totalMoveDuration - linearTime;
                float dampTime = time - linearTime;

                anim.States.MovementBlendValue = Mathf.Lerp(anim.States.MovementBlendValue, 0f, dampTime / dampDuration);
                anim.States.SlideBlendValue = Mathf.Lerp(anim.States.SlideBlendValue, 0f, dampTime / dampDuration);
            }

            UpdateLocalBlends(view, anim.States);
        }

        private static void UpdateLocalBlends(IAvatarView view, in AnimationStates animStates)
        {
            if (!animStates.IsGrounded)
                return;

            view.SetAnimatorFloat(AnimationHashes.MOVEMENT_BLEND, animStates.MovementBlendValue > BLEND_EPSILON ? animStates.MovementBlendValue : 0f);
            view.SetAnimatorFloat(AnimationHashes.SLIDE_BLEND, animStates.SlideBlendValue > BLEND_EPSILON ? animStates.SlideBlendValue : 0f);
        }
    }
}
