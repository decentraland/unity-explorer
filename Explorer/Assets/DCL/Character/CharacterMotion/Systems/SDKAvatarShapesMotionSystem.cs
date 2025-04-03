using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character.CharacterMotion.Components;
using DCL.Character.Components;
using DCL.CharacterMotion.Animation;
using DCL.CharacterMotion.Components;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using ECS.Abstract;
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
        private const float ROTATION_SPEED = 6.5f;
        private const float DOT_THRESHOLD = 0.999f;

        private SDKAvatarShapesMotionSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            UpdateMotionQuery(World, t);
        }

        [Query]
        [All(typeof(PBAvatarShape))]
        private void UpdateMotion(
            [Data] float deltaTime,
            in IAvatarView view,
            in CharacterTransform characterTransformComponent,
            ref CharacterInterpolationMovementComponent characterInterpolationMovementComponent,
            ref CharacterAnimationComponent animationComponent)
        {
            UpdatePosition(deltaTime, characterTransformComponent, in characterInterpolationMovementComponent);
            UpdateRotation(deltaTime, characterTransformComponent, in characterInterpolationMovementComponent);
            UpdateAnimations(deltaTime, view, in characterInterpolationMovementComponent, ref animationComponent);
            characterInterpolationMovementComponent.LastPosition = characterTransformComponent.Transform.position;
        }

        private static void UpdatePosition(
            float deltaTime,
            CharacterTransform characterTransformComponent,
            in CharacterInterpolationMovementComponent characterInterpolationMovementComponent)
        {
            float distanceToTarget = Vector3.Distance(characterInterpolationMovementComponent.TargetPosition, characterInterpolationMovementComponent.LastPosition);

            if (distanceToTarget < DISTANCE_EPSILON)
            {
                characterTransformComponent.Transform.position = characterInterpolationMovementComponent.TargetPosition;
                UpdateRotation(deltaTime, characterTransformComponent, characterInterpolationMovementComponent.TargetRotation);
                return;
            }

            // If the AvatarShape movement is already controlled by a tween, we skip it here
            if (characterInterpolationMovementComponent.IsPositionManagedByTween)
                return;

            float movementSpeed = WALK_SPEED;
            movementSpeed = distanceToTarget >= WALK_DISTANCE ?
                Mathf.MoveTowards(movementSpeed, RUN_SPEED, deltaTime * RUN_SPEED * 10) :
                Mathf.MoveTowards(movementSpeed, WALK_SPEED, deltaTime * RUN_SPEED * 30);

            Vector3 flattenDirection = characterInterpolationMovementComponent.LastPosition.GetYFlattenDirection(characterInterpolationMovementComponent.TargetPosition);
            Vector3 delta = flattenDirection.normalized * (movementSpeed * deltaTime);
            Vector3 directionVector = characterInterpolationMovementComponent.LastPosition.GetDirection(characterInterpolationMovementComponent.TargetPosition);

            // If we overshoot targetPosition, we adjust the delta value accordingly
            if (delta.sqrMagnitude > Vector3.SqrMagnitude(directionVector))
                delta = directionVector;

            characterTransformComponent.Transform.position = characterInterpolationMovementComponent.LastPosition + delta;
        }

        private static void UpdateRotation(
            float deltaTime,
            CharacterTransform characterTransformComponent,
            in CharacterInterpolationMovementComponent characterInterpolationMovementComponent)
        {
            // If the AvatarShape rotation is already controlled by a tween, we skip it here
            if (characterInterpolationMovementComponent.IsRotationManagedByTween)
                return;

            var flattenDirection = characterInterpolationMovementComponent.LastPosition.GetYFlattenDirection(characterTransformComponent.Transform.position);
            if (flattenDirection == Vector3.zero)
                return;

            Quaternion targetRotation = Quaternion.LookRotation(flattenDirection, Vector3.up);
            UpdateRotation(deltaTime, characterTransformComponent, targetRotation);
        }

        private static void UpdateRotation(float deltaTime, CharacterTransform characterTransformComponent, Quaternion targetRotation)
        {
            if (Quaternion.Dot(characterTransformComponent.Transform.rotation, targetRotation) > DOT_THRESHOLD)
                return;

            characterTransformComponent.Transform.rotation = Quaternion.Slerp(characterTransformComponent.Transform.rotation, targetRotation, ROTATION_SPEED * deltaTime);
        }

        private static void UpdateAnimations(
            float deltaTime,
            IAvatarView view,
            in CharacterInterpolationMovementComponent characterInterpolationMovementComponent,
            ref CharacterAnimationComponent animationComponent)
        {
            float distanceToTarget = Vector3.Distance(characterInterpolationMovementComponent.TargetPosition, characterInterpolationMovementComponent.LastPosition);
            float movementBlendValue = 0;

            if (distanceToTarget > 0)
            {
                float speed = Mathf.Round(distanceToTarget / deltaTime * 1000f) / 1000f;
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
