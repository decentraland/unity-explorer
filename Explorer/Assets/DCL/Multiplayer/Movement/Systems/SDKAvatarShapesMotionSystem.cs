using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character.Components;
using DCL.CharacterMotion.Animation;
using DCL.CharacterMotion.Components;
using DCL.Diagnostics;
using DCL.Profiles;
using ECS.Abstract;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class SDKAvatarShapesMotionSystem : BaseUnityLoopSystem
    {
        private const float RUN_SPEED_THRESHOLD = 9.5f;
        private const float JOG_SPEED_THRESHOLD = 4f;

        private SDKAvatarShapesMotionSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            UpdateMotionQuery(World);
        }

        [Query]
        [None(typeof(Profile))]
        private void UpdateMotion(
            in IAvatarView view,
            ref CharacterTransform characterTransformComponent,
            ref CharacterLastPosition characterLastPositionComponent,
            ref CharacterAnimationComponent animationComponent)
        {
            UpdateRotation(characterTransformComponent, ref characterLastPositionComponent);
            UpdateAnimations(view, characterTransformComponent, ref characterLastPositionComponent, ref animationComponent);
            UpdateLastPosition(ref characterTransformComponent, ref characterLastPositionComponent);
        }

        private static void UpdateRotation(
            CharacterTransform characterTransformComp,
            ref CharacterLastPosition characterLastPositionComp)
        {
            var currentPosition = characterTransformComp.Transform.position;
            var oldPosition = characterLastPositionComp.LastPosition;

            if (oldPosition == currentPosition)
                return;

            Vector3 newDirection = currentPosition - oldPosition;
            newDirection.y = 0;

            if (newDirection != Vector3.zero)
                characterTransformComp.Transform.forward = newDirection.normalized;
        }

        private static void UpdateAnimations(
            IAvatarView view,
            CharacterTransform characterTransformComp,
            ref CharacterLastPosition characterLastPositionComp,
            ref CharacterAnimationComponent animationComponent)
        {
            const bool IS_JUMP_TRIGGERED = false;
            const bool IS_STUNNED = false;

            var currentPosition = characterTransformComp.Transform.position;
            var oldPosition = characterLastPositionComp.LastPosition;
            float movementBlendValue = 0;

            if (oldPosition != currentPosition)
            {
                float speed = Vector3.Distance(oldPosition, currentPosition) / UnityEngine.Time.deltaTime;
                movementBlendValue = speed > RUN_SPEED_THRESHOLD ? 3 : speed > JOG_SPEED_THRESHOLD ? 2 : 1;
            }

            animationComponent.IsSliding = false;
            animationComponent.States.MovementBlendValue = movementBlendValue;
            animationComponent.States.SlideBlendValue = 0;
            animationComponent.States.IsGrounded = true;
            animationComponent.States.IsJumping = false;
            animationComponent.States.IsLongJump = false;
            animationComponent.States.IsLongFall = false;
            animationComponent.States.IsFalling = false;

            // movement blend
            AnimationMovementBlendLogic.SetAnimatorParameters(ref animationComponent, view, animationComponent.States.IsGrounded, 0);

            // slide
            AnimationSlideBlendLogic.SetAnimatorParameters(ref animationComponent, view);

            // other states
            AnimationStatesLogic.SetAnimatorParameters(view, ref animationComponent.States, animationComponent.States.IsJumping, IS_JUMP_TRIGGERED, IS_STUNNED);
        }

        private static void UpdateLastPosition(
            ref CharacterTransform characterTransformComponent,
            ref CharacterLastPosition characterLastPositionComponent) =>
            characterLastPositionComponent.LastPosition = characterTransformComponent.Transform.position;
    }
}
