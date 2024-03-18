using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character.Components;
using DCL.CharacterMotion.Animation;
using DCL.CharacterMotion.Components;
using DCL.Diagnostics;
using ECS.Abstract;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(RemotePlayersMovementSystem))]
    [LogCategory(ReportCategory.MULTIPLAYER_MOVEMENT)]
    public partial class RemotePlayerAnimationSystem : BaseUnityLoopSystem
    {
        public RemotePlayerAnimationSystem(World world) : base(world) { }

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
                InterpolateAnimations(deltaTime, intComp.TotalDuration, ref anim, intComp.Start.animState, intComp.End.animState);
            else if (extComp.Enabled)
                // Handle extrapolated animations (depending on speed)
                ;
        }

        private static void InterpolateAnimations(float t, float totalDuration, ref CharacterAnimationComponent anim, AnimationStates startState, AnimationStates endStates)
        {
            anim.States.MovementBlendValue = Mathf.Lerp(startState.MovementBlendValue, endStates.MovementBlendValue, t / totalDuration);
            anim.States.SlideBlendValue = Mathf.Lerp(startState.SlideBlendValue, endStates.SlideBlendValue, t / totalDuration);
        }

        private static void UpdateAnimations(IAvatarView view, ref CharacterAnimationComponent animationComponent, AnimationStates animState, bool isStunned)
        {
            if (animationComponent.States.Equals(animState))
                return;

            animationComponent.States = animState;

            view.SetAnimatorFloat(AnimationHashes.MOVEMENT_BLEND, animationComponent.States.MovementBlendValue);
            view.SetAnimatorFloat(AnimationHashes.SLIDE_BLEND, animationComponent.States.SlideBlendValue);

            if (view.GetAnimatorBool(AnimationHashes.JUMPING))
                view.SetAnimatorTrigger(AnimationHashes.JUMP);

            view.SetAnimatorBool(AnimationHashes.STUNNED, isStunned);
            view.SetAnimatorBool(AnimationHashes.GROUNDED, animationComponent.States.IsGrounded);
            view.SetAnimatorBool(AnimationHashes.JUMPING, animationComponent.States.IsJumping);
            view.SetAnimatorBool(AnimationHashes.FALLING, animationComponent.States.IsFalling);
            view.SetAnimatorBool(AnimationHashes.LONG_JUMP, animationComponent.States.IsLongJump);
            view.SetAnimatorBool(AnimationHashes.LONG_FALL, animationComponent.States.IsLongFall);
        }
    }
}
