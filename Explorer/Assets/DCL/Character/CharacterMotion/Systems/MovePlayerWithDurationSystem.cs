using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Character.Components;
using DCL.CharacterMotion.Components;
using DCL.Diagnostics;
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
        private const float ROTATION_SPEED = 6.5f;
        private const float DOT_THRESHOLD = 0.999f;

        private MovePlayerWithDurationSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            MovePlayerQuery(World, t);
        }

        [Query]
        private void MovePlayer(
            [Data] float deltaTime,
            Entity entity,
            ref PlayerMoveToWithDurationIntent moveIntent,
            ref CharacterController characterController,
            ref CharacterTransform characterTransform)
        {
            moveIntent.ElapsedTime += deltaTime;

            float progress = moveIntent.Progress;

            // Use smooth step for easing
            float smoothProgress = SmoothStep(progress);

            // Calculate new position
            Vector3 newPosition = Vector3.Lerp(moveIntent.StartPosition, moveIntent.TargetPosition, smoothProgress);

            // Disable CharacterController to allow direct position modification
            bool wasEnabled = characterController.enabled;
            characterController.enabled = false;

            characterTransform.Transform.position = newPosition;

            // Re-enable CharacterController
            characterController.enabled = wasEnabled;

            // Handle rotation towards target
            UpdateRotation(deltaTime, characterTransform, in moveIntent);

            // Check if movement is complete
            if (moveIntent.IsComplete)
            {
                // Ensure final position is exact
                characterController.enabled = false;
                characterTransform.Transform.position = moveIntent.TargetPosition;
                characterController.enabled = wasEnabled;

                // Apply final rotation if avatar target was specified
                ApplyFinalRotation(characterTransform, in moveIntent);

                // Remove the intent component
                World.Remove<PlayerMoveToWithDurationIntent>(entity);

                // Add MovePlayerToInfo to maintain compatibility with existing systems
                World.AddOrSet(entity, new MovePlayerToInfo(UnityEngine.Time.frameCount));
            }
        }

        private static void UpdateRotation(
            float deltaTime,
            CharacterTransform characterTransform,
            in PlayerMoveToWithDurationIntent moveIntent)
        {
            Vector3? targetPoint = moveIntent.AvatarTarget ?? moveIntent.CameraTarget;
            if (targetPoint == null)
                return;

            Vector3 currentPosition = characterTransform.Transform.position;
            Vector3 lookDirection = targetPoint.Value - currentPosition;
            lookDirection.y = 0; // Keep rotation horizontal

            if (lookDirection.sqrMagnitude < 0.0001f)
                return;

            Quaternion targetRotation = Quaternion.LookRotation(lookDirection, Vector3.up);

            if (Quaternion.Dot(characterTransform.Transform.rotation, targetRotation) > DOT_THRESHOLD)
                return;

            characterTransform.Transform.rotation = Quaternion.Slerp(
                characterTransform.Transform.rotation,
                targetRotation,
                ROTATION_SPEED * deltaTime);
        }

        private static void ApplyFinalRotation(
            CharacterTransform characterTransform,
            in PlayerMoveToWithDurationIntent moveIntent)
        {
            Vector3? targetPoint = moveIntent.AvatarTarget ?? moveIntent.CameraTarget;
            if (targetPoint == null)
                return;

            Vector3 lookDirection = targetPoint.Value - moveIntent.TargetPosition;
            lookDirection.y = 0;

            if (lookDirection.sqrMagnitude < 0.0001f)
                return;

            characterTransform.Transform.rotation = Quaternion.LookRotation(lookDirection, Vector3.up);
        }

        /// <summary>
        /// Smooth step function for easing (ease-in-out)
        /// </summary>
        private static float SmoothStep(float t)
        {
            return t * t * (3f - 2f * t);
        }
    }
}

