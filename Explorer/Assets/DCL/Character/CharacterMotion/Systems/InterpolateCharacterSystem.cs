using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Character.CharacterMotion.Components;
using DCL.CharacterCamera;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Platforms;
using DCL.CharacterMotion.Settings;
using ECS.Abstract;
using ECS.LifeCycle.Components;
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
    [UpdateInGroup(typeof(ChangeCharacterPositionGroup))]
    [UpdateBefore(typeof(CameraGroup))]
    public partial class InterpolateCharacterSystem : BaseUnityLoopSystem
    {
        private const float ALMOST_ZERO = 0.00001f;

        private int playerJustTeleportedCountDown;

        private InterpolateCharacterSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            InterpolateQuery(World, t);
        }

        [Query]
        [None(typeof(PlayerTeleportIntent), typeof(DeleteEntityIntention), typeof(PlayerTeleportIntent.JustTeleported))]
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
            ApplyVelocityStun.Execute(ref rigidTransform, in stunComponent);

            Transform characterTransform = characterController.transform;
            Vector3 movementDelta = rigidTransform.MoveVelocity.Velocity * dt;
            Vector3 gravityDelta = CalculateGravityDelta(dt, rigidTransform, platformComponent);
            Vector3 prevPos = characterTransform.position;

            // Force the platform collider to update its position, so slope modifier raycast can work properly
            if (platformComponent.IsMovingPlatform && platformComponent.PlatformCollider != null)
            {
                platformComponent.PlatformCollider.enabled = false;
                platformComponent.PlatformCollider.enabled = true;
            }

            Vector3 slopeModifier = ApplySlopeModifier.Execute(in settings, in rigidTransform, in movementInput, in jump, characterController, dt);

            CollisionFlags collisionFlags = characterController.Move(
                movementDelta
                + gravityDelta
                + slopeModifier
                + rigidTransform.PlatformDelta);

            Vector3 deltaMovement = characterTransform.position - prevPos;
            bool hasGroundedFlag = deltaMovement.y <= 0 && EnumUtils.HasFlag(collisionFlags, CollisionFlags.Below);

            if (!Mathf.Approximately(gravityDelta.y, 0f))
                rigidTransform.IsGrounded = hasGroundedFlag || characterController.isGrounded;

            rigidTransform.IsCollidingWithWall = EnumUtils.HasFlag(collisionFlags, CollisionFlags.Sides);

            // If we are on a platform we save our local position
            PlatformSaveLocalPosition.Execute(ref platformComponent, characterTransform.position);

            // In order to detect if we got stuck between 2 slopes we just check if our vertical delta movement is zero when on a slope
            if (rigidTransform.IsOnASteepSlope && Mathf.Abs(deltaMovement.sqrMagnitude) <= ALMOST_ZERO)
                rigidTransform.IsStuck = true;
            else
                rigidTransform.IsStuck = false;
        }

        private static Vector3 CalculateGravityDelta(float dt, CharacterRigidTransform rigidTransform, CharacterPlatformComponent platformComponent)
        {
            if (rigidTransform is { IsOnASteepSlope: true, IsStuck: false })
                return rigidTransform.SlopeGravity * dt;

            Vector3 finalGravity = rigidTransform.GravityVelocity * dt;

            if (platformComponent.IsMovingPlatform && rigidTransform.IsGrounded)
                finalGravity.y = 0f;

            return finalGravity;
        }
    }
}
