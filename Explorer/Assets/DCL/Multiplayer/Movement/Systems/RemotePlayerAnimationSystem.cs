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
using ECS.Abstract;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(RemotePlayersMovementSystem))]
    [LogCategory(ReportCategory.MULTIPLAYER_MOVEMENT)]
    public partial class RemotePlayerAnimationSystem : BaseUnityLoopSystem
    {
        private const float BLEND_EPSILON = 0.01f;
        private readonly RemotePlayerExtrapolationSettings settings;

        public RemotePlayerAnimationSystem(World world, RemotePlayerExtrapolationSettings settings) : base(world)
        {
            this.settings = settings;
        }

        protected override void Update(float t)
        {
            UpdatePlayersAnimationQuery(World, t);
        }

        [Query]
        [None(typeof(PlayerComponent))]
        private void UpdatePlayersAnimation([Data] float deltaTime, in IAvatarView view, ref CharacterAnimationComponent anim, ref RemotePlayerMovementComponent remotePlayerMovement
          , ref InterpolationComponent intComp, ref ExtrapolationComponent extComp)
        {
            if (remotePlayerMovement.RequireAnimationsUpdate)
            {
                remotePlayerMovement.RequireAnimationsUpdate = false;
                UpdateAnimations(view, ref anim, remotePlayerMovement.PastMessage.animState, remotePlayerMovement.PastMessage.isStunned);
            }

            if (intComp.Enabled)
                InterpolateAnimations(view, deltaTime, intComp.TotalDuration, ref anim, intComp.Start.animState, intComp.End.animState);
            else if (extComp.Enabled)
                ExtrapolateAnimations(view, ref anim, ref extComp, settings.LinearTime);
        }

        private static void ExtrapolateAnimations(IAvatarView view, ref CharacterAnimationComponent anim, ref ExtrapolationComponent extComp, float linearTime)
        {
            float time = extComp.Time;
            float totalMoveDuration = extComp.TotalMoveDuration;

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

            UpdateBlends(view, anim.States);
        }

        private static void InterpolateAnimations(IAvatarView view, float t, float totalDuration, ref CharacterAnimationComponent anim, AnimationStates startState,
            AnimationStates endStates)
        {
            anim.States.MovementBlendValue = Mathf.Lerp(startState.MovementBlendValue, endStates.MovementBlendValue, t / totalDuration);
            anim.States.SlideBlendValue = Mathf.Lerp(startState.SlideBlendValue, endStates.SlideBlendValue, t / totalDuration);

            UpdateBlends(view, anim.States);
        }

        private static void UpdateAnimations(IAvatarView view, ref CharacterAnimationComponent animationComponent, AnimationStates animState, bool isStunned)
        {
            if (animationComponent.States.Equals(animState))
                return;

            UpdateBlends(view, animState);

            if ((animationComponent.States.IsGrounded && !animState.IsGrounded) || (!animationComponent.States.IsJumping && animState.IsJumping))
                view.SetAnimatorTrigger(AnimationHashes.JUMP);

            view.SetAnimatorBool(AnimationHashes.STUNNED, isStunned);
            view.SetAnimatorBool(AnimationHashes.GROUNDED, animState.IsGrounded);
            view.SetAnimatorBool(AnimationHashes.JUMPING, animState.IsJumping);
            view.SetAnimatorBool(AnimationHashes.FALLING, animState.IsFalling);
            view.SetAnimatorBool(AnimationHashes.LONG_JUMP, animState.IsLongJump);
            view.SetAnimatorBool(AnimationHashes.LONG_FALL, animState.IsLongFall);

            animationComponent.States = animState;
        }

        private static void UpdateBlends(IAvatarView view, in AnimationStates animStates)
        {
            view.SetAnimatorFloat(AnimationHashes.MOVEMENT_BLEND, animStates.MovementBlendValue > BLEND_EPSILON ? animStates.MovementBlendValue : 0f);
            view.SetAnimatorFloat(AnimationHashes.SLIDE_BLEND, animStates.SlideBlendValue > BLEND_EPSILON ? animStates.SlideBlendValue : 0f);
        }
    }
}
