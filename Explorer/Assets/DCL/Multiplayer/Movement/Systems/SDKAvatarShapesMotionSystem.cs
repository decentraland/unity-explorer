using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character.Components;
using DCL.CharacterMotion.Animation;
using DCL.CharacterMotion.Components;
using DCL.Diagnostics;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using ECS.Abstract;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.AVATAR)]
    [ThrottlingEnabled]
    public partial class SDKAvatarShapesMotionSystem : BaseUnityLoopSystem
    {
        private const float ROTATION_SPEED = 5f;

        private SDKAvatarShapesMotionSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            UpdateMotionQuery(World);
        }

        [Query]
        private void UpdateMotion(
            in IAvatarView view,
            ref CharacterTransform characterTransformComponent,
            ref CharacterLastPosition characterLastPositionComponent,
            ref CharacterAnimationComponent animationComponent)
        {
            UpdateRotation(characterTransformComponent, ref characterLastPositionComponent);
            UpdateAnimations(view, characterTransformComponent, ref characterLastPositionComponent, ref animationComponent);
            characterLastPositionComponent.LastPosition = characterTransformComponent.Transform.position;
        }

        private static void UpdateRotation(
            CharacterTransform characterTransformComp,
            ref CharacterLastPosition characterLastPositionComp)
        {
            var currentPosition = characterTransformComp.Transform.position;
            var oldPosition = characterLastPositionComp.LastPosition;

            if (oldPosition == currentPosition)
                return;

            Vector3 newDirection = oldPosition.GetYFlattenDirection(currentPosition);
            if (newDirection != Vector3.zero)
            {
                Vector3 currentForward = characterTransformComp.Transform.forward;

                if (Vector3.Dot(currentForward, newDirection) < 0.999f)
                    characterTransformComp.Transform.forward = Vector3.Lerp(currentForward, newDirection, UnityEngine.Time.deltaTime * ROTATION_SPEED);
            }
        }

        private static void UpdateAnimations(
            IAvatarView view,
            CharacterTransform characterTransformComp,
            ref CharacterLastPosition characterLastPositionComp,
            ref CharacterAnimationComponent animationComponent)
        {
            var currentPosition = characterTransformComp.Transform.position;
            var oldPosition = characterLastPositionComp.LastPosition;
            float movementBlendValue = 0;

            if (oldPosition != currentPosition)
            {
                float speed = Vector3.Distance(oldPosition, currentPosition) / UnityEngine.Time.deltaTime;
                movementBlendValue = RemotePlayerUtils.GetBlendValueFromSpeed(speed);
            }

            SetGroundedMovement(ref animationComponent, movementBlendValue);

            // movement blend
            AnimationMovementBlendLogic.SetAnimatorParameters(ref animationComponent, view, animationComponent.States.IsGrounded, 0);

            // slide
            AnimationSlideBlendLogic.SetAnimatorParameters(ref animationComponent, view);

            // other states
            AnimationStatesLogic.SetAnimatorParameters(view, ref animationComponent.States, animationComponent.States.IsJumping, jumpTriggered: false, isStunned: false);
        }

        private static void SetGroundedMovement(ref CharacterAnimationComponent animationComponent, float movementBlendValue)
        {
            animationComponent.IsSliding = false;
            animationComponent.States.MovementBlendValue = movementBlendValue;
            animationComponent.States.SlideBlendValue = 0;
            animationComponent.States.IsGrounded = true;
            animationComponent.States.IsJumping = false;
            animationComponent.States.IsLongJump = false;
            animationComponent.States.IsLongFall = false;
            animationComponent.States.IsFalling = false;
        }
    }
}
