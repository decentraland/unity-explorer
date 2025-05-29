using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character.Components;
using DCL.CharacterMotion.Animation;
using DCL.CharacterMotion.Components;
using DCL.Diagnostics;
using DCL.Multiplayer.Movement.Settings;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using System;
using UnityEngine;
using Utility.Animations;
using static DCL.CharacterMotion.Animation.AnimationMovementBlendLogic;

namespace DCL.Multiplayer.Movement.Systems
{
    [UpdateInGroup(typeof(PostPhysicsSystemGroup))]
    [UpdateAfter(typeof(RemotePlayersMovementSystem))]
    [LogCategory(ReportCategory.MULTIPLAYER_MOVEMENT)]
    public partial class RemotePlayerAnimationSystem : BaseUnityLoopSystem
    {
        private readonly RemotePlayerExtrapolationSettings extrapolationSettings;
        private readonly IMultiplayerMovementSettings movementSettings;

        public RemotePlayerAnimationSystem(World world, RemotePlayerExtrapolationSettings extrapolationSettings, IMultiplayerMovementSettings movementSettings) : base(world)
        {
            this.extrapolationSettings = extrapolationSettings;
            this.movementSettings = movementSettings;
        }

        protected override void Update(float t)
        {
            UpdatePlayersAnimationQuery(World);
        }

        [Query]
        [None(typeof(PlayerComponent), typeof(DeleteEntityIntention))]
        private void UpdatePlayersAnimation(in IAvatarView view, ref CharacterAnimationComponent anim,
            ref RemotePlayerMovementComponent remotePlayerMovement, ref InterpolationComponent intComp, ref ExtrapolationComponent extComp)
        {
            // When we finally pass the message, we set all Animator parameters from this snapshot
            if (remotePlayerMovement.WasPassedThisFrame)
            {
                remotePlayerMovement.WasPassedThisFrame = false;
                UpdateAnimations(view, ref anim, ref remotePlayerMovement.PastMessage);
            }

            if (intComp.Enabled)
                InterpolateAnimations(view, ref anim, intComp);
            else if (extComp.Enabled)
                ExtrapolateAnimations(view, ref anim, extComp.Time, extComp.TotalMoveDuration, extrapolationSettings.LinearTime);
            else
            {
                anim.States.MovementBlendValue -= movementSettings.IdleSlowDownSpeed * UnityEngine.Time.deltaTime;
                anim.States.SlideBlendValue -= movementSettings.IdleSlowDownSpeed * UnityEngine.Time.deltaTime;

                view.SetAnimatorFloat(AnimationHashes.MOVEMENT_BLEND, anim.States.MovementBlendValue.ClampSmallValuesToZero(BLEND_EPSILON));
                view.SetAnimatorFloat(AnimationHashes.SLIDE_BLEND, anim.States.SlideBlendValue.ClampSmallValuesToZero(BLEND_EPSILON));
            }
        }

        private static void UpdateAnimations(IAvatarView view, ref CharacterAnimationComponent animationComponent, ref NetworkMovementMessage message)
        {
            if (animationComponent.States.Equals(message.animState))
                return;

            // movement blend
            animationComponent.States.MovementBlendValue = message.animState.MovementBlendValue;
            AnimationMovementBlendLogic.SetAnimatorParameters(ref animationComponent, view, message.animState.IsGrounded, (int)message.movementKind);

            // slide
            animationComponent.IsSliding = message.isSliding;
            animationComponent.States.SlideBlendValue = message.animState.SlideBlendValue;
            AnimationSlideBlendLogic.SetAnimatorParameters(ref animationComponent, view);

            // other states
            bool jumpTriggered = (animationComponent.States.IsGrounded && !message.animState.IsGrounded) || (!animationComponent.States.IsJumping && message.animState.IsJumping);
            animationComponent.States.IsGrounded = message.animState.IsGrounded;
            animationComponent.States.IsJumping = message.animState.IsJumping;
            animationComponent.States.IsLongJump = message.animState.IsLongJump;
            animationComponent.States.IsFalling = message.animState.IsFalling;
            animationComponent.States.IsLongFall = message.animState.IsLongFall;
            AnimationStatesLogic.SetAnimatorParameters(view, ref animationComponent.States, animationComponent.States.IsJumping, jumpTriggered, message.isStunned);
        }

        private static void InterpolateAnimations(IAvatarView view, ref CharacterAnimationComponent anim, in InterpolationComponent intComp)
        {
            if (!anim.States.IsJumping && intComp.End.animState.IsJumping && Mathf.Abs(intComp.Start.position.y - intComp.End.position.y) > RemotePlayerUtils.JUMP_EPSILON)
                AnimateFutureJump(view, ref anim, intComp.End.animState);

            AnimationStates startAnimStates = intComp.Start.animState;
            AnimationStates endAnimStates = intComp.End.animState;

            bool bothPointBlendsAreZero = startAnimStates.MovementBlendValue < BLEND_EPSILON && endAnimStates.MovementBlendValue < BLEND_EPSILON
                        && startAnimStates.SlideBlendValue < BLEND_EPSILON && endAnimStates.SlideBlendValue < BLEND_EPSILON;

            bool isNotOnPlatform = intComp.Start.syncedPlatform == null
                                   || intComp.Start.syncedPlatform.Value.EntityId == uint.MaxValue
                                   ||intComp.End.syncedPlatform == null || intComp.End.syncedPlatform.Value.EntityId == uint.MaxValue;

            if (bothPointBlendsAreZero && isNotOnPlatform
                && Vector3.SqrMagnitude(intComp.Start.position - intComp.End.position) > RemotePlayerUtils.MOVEMENT_EPSILON)
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
            float midPointBlendValue = RemotePlayerUtils.GetBlendValueFromSpeed(speed);

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

            view.SetAnimatorFloat(AnimationHashes.MOVEMENT_BLEND, animStates.MovementBlendValue.ClampSmallValuesToZero(BLEND_EPSILON));
            view.SetAnimatorFloat(AnimationHashes.SLIDE_BLEND, animStates.SlideBlendValue.ClampSmallValuesToZero(BLEND_EPSILON));
        }
    }
}
