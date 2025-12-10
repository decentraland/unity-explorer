using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
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
        private const float DOT_THRESHOLD = 0.999f;

        private MovePlayerWithDurationSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            InterruptMovementOnInputQuery(World);
            MovePlayerQuery(World, t);
        }

        [Query]
        [All(typeof(PlayerMoveToWithDurationIntent))]
        private void InterruptMovementOnInput(Entity entity, in MovementInputComponent movementInputComponent)
        {
            if (movementInputComponent.Kind == MovementKind.IDLE || movementInputComponent.Axes == Vector2.zero)
                return;

            World.Remove<PlayerMoveToWithDurationIntent>(entity);
        }

        [Query]
        private void MovePlayer(
            [Data] float deltaTime,
            Entity entity,
            ref PlayerMoveToWithDurationIntent moveIntent,
            ref CharacterController characterController,
            ref CharacterTransform characterTransform,
            ref CharacterAnimationComponent animationComponent,
            in IAvatarView avatarView)
        {
            // Check if this is the first frame (before incrementing elapsed time)
            bool isFirstFrame = moveIntent.ElapsedTime == 0f;

            moveIntent.ElapsedTime += deltaTime;

            float progress = moveIntent.Progress;

            // Use smooth step for easing
            float smoothProgress = SmoothStep(progress);

            // Calculate new position
            Vector3 newPosition = Vector3.Lerp(moveIntent.StartPosition, moveIntent.TargetPosition, smoothProgress);

            // Disable CharacterController to allow direct position modification
            // TODO: is this needed??
            bool wasEnabled = characterController.enabled;
            characterController.enabled = false;

            characterTransform.Transform.position = newPosition;

            // Re-enable CharacterController
            characterController.enabled = wasEnabled;

            // Handle rotation: immediately face movement direction on first frame, then maintain it
            ApplyMovementDirectionRotation(characterTransform, in moveIntent, isFirstFrame);

            // Update animation based on movement speed
            UpdateAnimation(deltaTime, avatarView, ref animationComponent, ref moveIntent, newPosition);

            // Check if movement is complete
            if (moveIntent.IsComplete)
            {
                // Ensure final position is exact
                characterController.enabled = false;
                characterTransform.Transform.position = moveIntent.TargetPosition;
                characterController.enabled = wasEnabled;

                // Apply final rotation instantly if avatar target was specified
                ApplyFinalRotation(characterTransform, in moveIntent);

                // Reset animation to idle
                ResetAnimationToIdle(avatarView, ref animationComponent);

                // Remove the intent component
                World.Remove<PlayerMoveToWithDurationIntent>(entity);

                // Add MovePlayerToInfo to maintain compatibility with existing systems
                World.AddOrSet(entity, new MovePlayerToInfo(UnityEngine.Time.frameCount));
            }
        }

        /// <summary>
        /// Immediately faces the movement direction (from start to target).
        /// Applied instantly on first frame, maintained during movement.
        /// </summary>
        private static void ApplyMovementDirectionRotation(
            CharacterTransform characterTransform,
            in PlayerMoveToWithDurationIntent moveIntent,
            bool instant)
        {
            Vector3 movementDirection = moveIntent.TargetPosition - moveIntent.StartPosition;
            movementDirection.y = 0; // Keep rotation horizontal

            if (movementDirection.sqrMagnitude < 0.0001f)
                return;

            Quaternion targetRotation = Quaternion.LookRotation(movementDirection, Vector3.up);

            if (instant)
            {
                // Instantly face movement direction on first frame
                characterTransform.Transform.rotation = targetRotation;
            }
            else if (Quaternion.Dot(characterTransform.Transform.rotation, targetRotation) < DOT_THRESHOLD)
            {
                // Maintain facing movement direction (shouldn't need much correction after initial snap)
                characterTransform.Transform.rotation = targetRotation;
            }
        }

        /// <summary>
        /// Applies final rotation instantly when movement completes.
        /// If avatarTarget exists, faces that; otherwise no rotation change.
        /// </summary>
        private static void ApplyFinalRotation(
            CharacterTransform characterTransform,
            in PlayerMoveToWithDurationIntent moveIntent)
        {
            // Only apply final rotation if avatarTarget is specified
            if (moveIntent.AvatarTarget == null)
                return;

            Vector3 lookDirection = moveIntent.AvatarTarget.Value - moveIntent.TargetPosition;
            lookDirection.y = 0;

            if (lookDirection.sqrMagnitude < 0.0001f)
                return;

            // Instantly snap to face avatar target
            characterTransform.Transform.rotation = Quaternion.LookRotation(lookDirection, Vector3.up);
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
            animationComponent.IsSliding = false;
            animationComponent.States.MovementBlendValue = movementBlendValue;
            animationComponent.States.SlideBlendValue = 0;
            animationComponent.States.IsGrounded = true;
            animationComponent.States.IsJumping = false;
            animationComponent.States.IsLongJump = false;
            animationComponent.States.IsLongFall = false;
            animationComponent.States.IsFalling = false;

            // Apply animator parameters
            AnimationMovementBlendLogic.SetAnimatorParameters(ref animationComponent, avatarView, isGrounded: true, movementBlendId: 0);
            AnimationSlideBlendLogic.SetAnimatorParameters(ref animationComponent, avatarView);
            AnimationStatesLogic.SetAnimatorParameters(avatarView, ref animationComponent.States, isJumping: false, jumpTriggered: false, isStunned: false);
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

