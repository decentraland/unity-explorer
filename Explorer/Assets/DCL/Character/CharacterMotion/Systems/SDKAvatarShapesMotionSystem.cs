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
using System;
using UnityEngine;

namespace DCL.Character.CharacterMotion.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.AVATAR)]
    [ThrottlingEnabled]
    public partial class SDKAvatarShapesMotionSystem : BaseUnityLoopSystem
    {
        private const float DISTANCE_EPSILON = 0.0001f;
        private const float WALK_DISTANCE = 1.5f;
        private const float WALK_SPEED = 4f;
        private const float RUN_SPEED = 10f;
        private const float ROTATION_SPEED = 10f;
        private const float DOT_THRESHOLD = 0.999f;

        private SDKAvatarShapesMotionSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            UpdateMotionQuery(World);
        }

        [Query]
        private void UpdateMotion(
            in IAvatarView view,
            ref CharacterTransform characterTransformComponent,
            ref CharacterTargetPositionComponent characterTargetPositionComponent,
            ref CharacterAnimationComponent animationComponent)
        {
            UpdatePosition(characterTransformComponent, ref characterTargetPositionComponent);
            UpdateRotation(characterTransformComponent, ref characterTargetPositionComponent);
            UpdateAnimations(view, characterTransformComponent, ref characterTargetPositionComponent, ref animationComponent);
            characterTargetPositionComponent.LastPosition = characterTransformComponent.Transform.position;
        }

        private static void UpdatePosition(
            CharacterTransform characterTransformComponent,
            ref CharacterTargetPositionComponent characterTargetPositionComponent)
        {
            float distanceToTarget = Vector3.Distance(characterTargetPositionComponent.TargetPosition, characterTargetPositionComponent.LastPosition);

            if (distanceToTarget < DISTANCE_EPSILON)
            {
                characterTransformComponent.Transform.position = characterTargetPositionComponent.TargetPosition;
                UpdateRotation(characterTransformComponent, characterTargetPositionComponent.FinalRotation);
                return;
            }

            // If the AvatarShape movement is already controlled by a tween, we skip the interpolation
            if (characterTargetPositionComponent.IsManagedByTween)
                return;

            float movementSpeed = WALK_SPEED;
            movementSpeed = distanceToTarget >= WALK_DISTANCE ?
                Mathf.MoveTowards(movementSpeed, RUN_SPEED, UnityEngine.Time.deltaTime * RUN_SPEED * 10) :
                Mathf.MoveTowards(movementSpeed, WALK_SPEED, UnityEngine.Time.deltaTime * RUN_SPEED * 30);

            Vector3 flattenDirection = characterTargetPositionComponent.LastPosition.GetYFlattenDirection(characterTargetPositionComponent.TargetPosition);
            Vector3 delta = flattenDirection.normalized * (movementSpeed * UnityEngine.Time.deltaTime);
            Vector3 directionVector = characterTargetPositionComponent.LastPosition.GetDirection(characterTargetPositionComponent.TargetPosition);

            // If we overshoot targetPosition, we adjust the delta value accordingly
            if (delta.sqrMagnitude > Vector3.SqrMagnitude(directionVector))
                delta = directionVector;

            characterTransformComponent.Transform.position = characterTargetPositionComponent.LastPosition + delta;
        }

        private static void UpdateRotation(
            CharacterTransform characterTransformComponent,
            ref CharacterTargetPositionComponent characterTargetPositionComponent)
        {
            var flattenDirection = characterTargetPositionComponent.LastPosition.GetYFlattenDirection(characterTransformComponent.Transform.position);
            if (flattenDirection == Vector3.zero)
                return;

            Quaternion targetRotation = Quaternion.LookRotation(flattenDirection, Vector3.up);
            UpdateRotation(characterTransformComponent, targetRotation);
        }

        private static void UpdateRotation(CharacterTransform characterTransformComponent, Quaternion targetRotation)
        {
            if (Quaternion.Dot(characterTransformComponent.Transform.rotation, targetRotation) > DOT_THRESHOLD)
                return;

            characterTransformComponent.Transform.rotation = Quaternion.Slerp(characterTransformComponent.Transform.rotation, targetRotation, ROTATION_SPEED * UnityEngine.Time.deltaTime);
        }

        private static void UpdateAnimations(
            IAvatarView view,
            CharacterTransform characterTransformComponent,
            ref CharacterTargetPositionComponent characterTargetPositionComponent,
            ref CharacterAnimationComponent animationComponent)
        {
            float distanceToTarget = Vector3.Distance(characterTransformComponent.Transform.position, characterTargetPositionComponent.LastPosition);
            float movementBlendValue = 0;

            if (distanceToTarget > 0)
            {
                float speed = distanceToTarget / UnityEngine.Time.deltaTime;
                speed = (float)Math.Round(speed, 3);
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
