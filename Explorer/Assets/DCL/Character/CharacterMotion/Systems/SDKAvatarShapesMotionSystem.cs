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
using ECS.Abstract;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.AVATAR)]
    [ThrottlingEnabled]
    public partial class SDKAvatarShapesMotionSystem : BaseUnityLoopSystem
    {
        private const float DISTANCE_EPSILON = 0.0001f;
        private const float MAX_DISTANCE_FOR_INTERPOLATION = 50f;
        private const float WALK_DISTANCE = 1.5f;
        private const float WALK_SPEED = 4f;
        private const float RUN_SPEED = 10.0f;
        private const float ROTATION_SPEED = 6.25f;

        private SDKAvatarShapesMotionSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            UpdateMotionQuery(World);
        }

        [Query]
        private void UpdateMotion(
            in IAvatarView view,
            ref CharacterTransform characterTransformComponent,
            ref CharacterTargetPosition characterTargetPositionComponent,
            ref CharacterAnimationComponent animationComponent)
        {
            UpdatePosition(characterTransformComponent, ref characterTargetPositionComponent);
            UpdateRotation(characterTransformComponent, ref characterTargetPositionComponent);
            UpdateAnimations(view, ref characterTargetPositionComponent, ref animationComponent);
            characterTargetPositionComponent.LastPosition = characterTransformComponent.Transform.position;
        }

        private static void UpdatePosition(
            CharacterTransform characterTransformComp,
            ref CharacterTargetPosition characterTargetPositionComp)
        {
            float distanceToTarget = characterTargetPositionComp.DistanceToTarget;

            switch (distanceToTarget)
            {
                case 0:
                    characterTransformComp.Transform.rotation = characterTargetPositionComp.FinalRotation;
                    return;
                case >= MAX_DISTANCE_FOR_INTERPOLATION or < DISTANCE_EPSILON:
                    characterTransformComp.Transform.position = characterTargetPositionComp.TargetPosition;
                    return;
            }

            float movementSpeed = WALK_SPEED;
            movementSpeed = distanceToTarget >= WALK_DISTANCE ?
                Mathf.MoveTowards(movementSpeed, RUN_SPEED, UnityEngine.Time.deltaTime * RUN_SPEED * 10) :
                Mathf.MoveTowards(movementSpeed, WALK_SPEED, UnityEngine.Time.deltaTime * RUN_SPEED * 30);

            Vector3 flattenDirection = characterTargetPositionComp.FlattenDirectionVector;
            Vector3 delta = flattenDirection.normalized * (movementSpeed * UnityEngine.Time.deltaTime);

            // If we overshoot targetPosition, we adjust the delta value accordingly
            if (delta.sqrMagnitude > Vector3.SqrMagnitude(characterTargetPositionComp.DirectionVector))
                delta = characterTargetPositionComp.DirectionVector;

            characterTransformComp.Transform.position = characterTargetPositionComp.LastPosition + delta;
        }

        private static void UpdateRotation(
            CharacterTransform characterTransformComp,
            ref CharacterTargetPosition characterTargetPositionComp)
        {
            if (characterTargetPositionComp.FlattenDirectionVector == Vector3.zero)
                return;

            Quaternion targetRotation = Quaternion.LookRotation(characterTargetPositionComp.FlattenDirectionVector, Vector3.up);
            characterTransformComp.Transform.rotation = Quaternion.Slerp(characterTransformComp.Transform.rotation, targetRotation, ROTATION_SPEED * UnityEngine.Time.deltaTime);
        }

        private static void UpdateAnimations(
            IAvatarView view,
            ref CharacterTargetPosition characterTargetPositionComp,
            ref CharacterAnimationComponent animationComponent)
        {
            float distanceToTarget = characterTargetPositionComp.DistanceToTarget;
            float movementBlendValue = 0;

            if (distanceToTarget > 0)
            {
                float speed = distanceToTarget / UnityEngine.Time.deltaTime;
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
