using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character.CharacterMotion.Components;
using DCL.Character.Components;
using DCL.CharacterMotion.Animation;
using DCL.CharacterMotion.Components;
using DCL.Diagnostics;
using DCL.Utilities;
using ECS.Abstract;
using UnityEngine;
using Utility.Arch;

namespace DCL.CharacterMotion.Systems
{
    /// <summary>
    /// Handles smooth player movement over a specified duration.
    /// Bypasses physics and directly interpolates the transform position.
    /// Mutually exclusive with <see cref="InterpolateCharacterSystem"/> and <see cref="TeleportCharacterSystem"/>.
    /// </summary>
    [UpdateInGroup(typeof(ChangeCharacterPositionGroup))]
    [UpdateAfter(typeof(TeleportCharacterSystem))]
    [LogCategory(ReportCategory.MOTION)]
    public partial class MovePlayerWithDurationSystem : BaseUnityLoopSystem
    {
        internal MovePlayerWithDurationSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            InterruptMovementOnInputQuery(World);
            MovePlayerQuery(World, t);
        }

        [Query]
        private void InterruptMovementOnInput(Entity entity, in MovementInputComponent movementInputComponent, in JumpInputComponent jumpInputComponent, ref PlayerMoveToWithDurationIntent moveIntent)
        {
            bool hasMovementInput = movementInputComponent.Kind != MovementKind.IDLE && movementInputComponent.Axes != Vector2.zero;
            bool hasJumpInput = jumpInputComponent.IsPressed;

            if (!hasMovementInput && !hasJumpInput)
                return;

            moveIntent.CompletionSource.TrySetResult(false);
            World.Remove<PlayerMoveToWithDurationIntent>(entity);
        }

        [Query]
        private void MovePlayer(
            [Data] float deltaTime,
            Entity entity,
            ref PlayerMoveToWithDurationIntent moveIntent,
            ref CharacterTransform characterTransform,
            ref CharacterRigidTransform rigidTransform,
            ref CharacterAnimationComponent animationComponent,
            in IAvatarView avatarView)
        {
            // Always enforce the movement direction rotation every frame
            // This ensures consistent behavior even if the component is added mid-frame
            ApplyMovementDirectionRotation(ref characterTransform, ref rigidTransform, in moveIntent);

            moveIntent.ElapsedTime += deltaTime;

            float progress = moveIntent.Progress;

            // Smooth step for easing
            float smoothProgress = SmoothStep(progress);
            Vector3 newPosition = Vector3.Lerp(moveIntent.StartPosition, moveIntent.TargetPosition, smoothProgress);

            // We intentionally bypass CharacterController.Move() to avoid physics/collision detection.
            // This allows the player to move smoothly to the target position without being blocked by obstacles.
            characterTransform.SetPositionWithDirtyCheck(newPosition);

            // Update animation based on movement speed
            UpdateAnimation(deltaTime, avatarView, ref animationComponent, ref moveIntent, newPosition);

            if (moveIntent.IsComplete)
            {
                // Ensure final position is exact
                characterTransform.SetPositionWithDirtyCheck(moveIntent.TargetPosition);

                ApplyFinalRotation(ref characterTransform, ref rigidTransform, in moveIntent);
                ResetAnimationToIdle(avatarView, ref animationComponent);

                moveIntent.CompletionSource.TrySetResult(true);
                World.Remove<PlayerMoveToWithDurationIntent>(entity);
            }
        }

        /// <summary>
        /// Immediately faces the movement direction (from start to target).
        /// Sets both the transform rotation and the LookDirection in CharacterRigidTransform.
        /// </summary>
        private static void ApplyMovementDirectionRotation(
            ref CharacterTransform characterTransform,
            ref CharacterRigidTransform rigidTransform,
            in PlayerMoveToWithDurationIntent moveIntent)
        {
            Vector3 movementDirection = moveIntent.TargetPosition - moveIntent.StartPosition;
            movementDirection.y = 0; // Keep rotation horizontal

            if (movementDirection.sqrMagnitude < 0.0001f)
                return;

            Vector3 normalizedDirection = movementDirection.normalized;

            // Set the LookDirection so RotateCharacterSystem maintains this rotation
            rigidTransform.LookDirection = normalizedDirection;

            // Also set the transform rotation immediately
            characterTransform.SetRotation(Quaternion.LookRotation(normalizedDirection, Vector3.up));
        }

        /// <summary>
        /// Applies final rotation instantly when movement completes.
        /// If avatarTarget exists, faces that; otherwise no rotation change.
        /// Sets both the transform rotation and the LookDirection in CharacterRigidTransform.
        /// </summary>
        private static void ApplyFinalRotation(
            ref CharacterTransform characterTransform,
            ref CharacterRigidTransform rigidTransform,
            in PlayerMoveToWithDurationIntent moveIntent)
        {
            // Only apply final rotation if avatarTarget is specified
            if (moveIntent.AvatarTarget == null)
                return;

            Vector3 lookDirection = moveIntent.AvatarTarget.Value - moveIntent.TargetPosition;
            lookDirection.y = 0;

            if (lookDirection.sqrMagnitude < 0.0001f)
                return;

            Vector3 normalizedDirection = lookDirection.normalized;

            // Set the LookDirection so RotateCharacterSystem maintains this rotation
            rigidTransform.LookDirection = normalizedDirection;

            // Instantly snap to face avatar target
            characterTransform.SetRotation(Quaternion.LookRotation(normalizedDirection, Vector3.up));
        }

        private static void UpdateAnimation(
            float deltaTime,
            IAvatarView avatarView,
            ref CharacterAnimationComponent animationComponent,
            ref PlayerMoveToWithDurationIntent moveIntent,
            Vector3 currentPosition)
        {
            // Calculate speed based on actual position change
            float distance = Vector3.Distance(currentPosition, moveIntent.LastFramePosition);
            float speed = deltaTime > 0 ? distance / deltaTime : 0f;

            // Update last frame position for next frame's calculation
            moveIntent.LastFramePosition = currentPosition;

            // Get blend value from speed (0 = idle, 1 = walk, 2 = jog, 3 = run)
            float movementBlendValue = speed > 0.01f ? RemotePlayerUtils.GetBlendValueFromSpeed(speed) : 0f;

            // Set animation state as grounded movement
            animationComponent.States.MovementBlendValue = movementBlendValue;
            animationComponent.States.IsSliding = false;
            animationComponent.States.SlideBlendValue = 0;
            animationComponent.States.IsGrounded = true;
            animationComponent.States.JumpCount = 0;
            animationComponent.States.IsLongJump = false;
            animationComponent.States.IsLongFall = false;
            animationComponent.States.IsFalling = false;

            // Apply animator parameters
            AnimationMovementBlendLogic.SetAnimatorParameters(ref animationComponent, avatarView, isGrounded: true, movementBlendId: 0);
            AnimationSlideBlendLogic.SetAnimatorParameters(ref animationComponent, avatarView);
            AnimationStatesLogic.SetAnimatorParameters(avatarView, animationComponent.States, jumpTriggered: false);
        }

        private static void ResetAnimationToIdle(
            IAvatarView avatarView,
            ref CharacterAnimationComponent animationComponent)
        {
            // Reset to idle state
            animationComponent.States.MovementBlendValue = 0f;
            animationComponent.States.IsGrounded = true;

            AnimationMovementBlendLogic.SetAnimatorParameters(ref animationComponent, avatarView, isGrounded: true, movementBlendId: 0);
        }

        // TODO: Do we want this easing??
        /// <summary>
        /// Smooth step function for easing (ease-in-out)
        /// </summary>
        private static float SmoothStep(float t)
            => t * t * (3f - (2f * t));
    }
}

