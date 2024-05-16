using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Character.CharacterMotion.Components;
using DCL.CharacterCamera;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Platforms;
using DCL.CharacterMotion.Settings;
using DCL.Diagnostics;
using ECS.Abstract;
using UnityEngine;
using Utility;

namespace DCL.CharacterMotion.Systems
{
    /// <summary>
    ///     Handles interpolating the character during variable update.
    ///     <para>
    ///         Modifies Transform (by calling `CharacterController` so the value is exposed to other systems ignoring Physics behind
    ///     </para>
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.MOTION)]
    [UpdateBefore(typeof(CameraGroup))]
    public partial class InterpolateCharacterSystem : BaseUnityLoopSystem
    {
        private const float ALMOST_ZERO = 0.00001f;
        private bool playerHasJustTeleported;

        private InterpolateCharacterSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            InterpolateQuery(World, t);
            TeleportPlayerQuery(World);
        }

        [Query]
        private void TeleportPlayer(in Entity entity, in CharacterController controller, ref CharacterPlatformComponent platformComponent, in PlayerTeleportIntent teleportIntent)
        {
            if (teleportIntent.LoadReport != null && teleportIntent.LoadReport.CompletionSource.UnsafeGetStatus() == UniTaskStatus.Pending)
                return;

            playerHasJustTeleported = true;

            // Teleport the character
            controller.transform.position = teleportIntent.Position;

            // Reset the current platform so we dont bounce back if we are touching the world plane
            platformComponent.CurrentPlatform = null;

            World.Remove<PlayerTeleportIntent>(entity);
        }

        [Query]
        [None(typeof(PlayerTeleportIntent))]
        private void Interpolate(
            [Data] float dt,
            in ICharacterControllerSettings settings,
            ref CharacterRigidTransform rigidTransform,
            ref CharacterController characterController,
            ref CharacterPlatformComponent platformComponent,
            ref StunComponent stunComponent,
            in JumpInputComponent jump,
            in MovementInputComponent movementInput)
        {
            if (playerHasJustTeleported)
            {
                // We need to skip the first frame after a teleport to avoid getting conflicts with the interpolation.
                // This sometimes provoked the teleport to be ignored and the character to be stuck in the previous position.
                playerHasJustTeleported = false;
                return;
            }

            Transform transform = characterController.transform;
            Vector3 slopeModifier = ApplySlopeModifier.Execute(in settings, in rigidTransform, in movementInput, in jump, characterController, dt);

            ApplyVelocityStun.Execute(ref rigidTransform, in stunComponent);

            Vector3 movementDelta = rigidTransform.MoveVelocity.Velocity * dt;
            Vector3 finalGravity = rigidTransform.IsOnASteepSlope && !rigidTransform.IsStuck ? rigidTransform.SlopeGravity : rigidTransform.GravityVelocity;
            Vector3 gravityDelta = finalGravity * dt;
            Vector3 platformDelta = rigidTransform.PlatformDelta;

            // In order for some systems to work correctly we move the character horizontally and then vertically
            Vector3 prevPos = transform.position;
            CollisionFlags collisionFlags = characterController.Move(movementDelta + gravityDelta + slopeModifier + platformDelta);
            Vector3 deltaMovement = transform.position - prevPos;

            bool hasGroundedFlag = deltaMovement.y <= 0 && EnumUtils.HasFlag(collisionFlags, CollisionFlags.Below);

            if (!Mathf.Approximately(gravityDelta.y, 0f))
                rigidTransform.IsGrounded = hasGroundedFlag || characterController.isGrounded;

            rigidTransform.IsCollidingWithWall = EnumUtils.HasFlag(collisionFlags, CollisionFlags.Sides);

            // If we are on a platform we save our local position
            PlatformSaveLocalPosition.Execute(ref platformComponent, transform.position);

            // In order to detect if we got stuck between 2 slopes we just check if our vertical delta movement is zero when on a slope
            if (rigidTransform.IsOnASteepSlope && Mathf.Abs(deltaMovement.sqrMagnitude) <= ALMOST_ZERO)
                rigidTransform.IsStuck = true;
            else
                rigidTransform.IsStuck = false;
        }
    }
}
