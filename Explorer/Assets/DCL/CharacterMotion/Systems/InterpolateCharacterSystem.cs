using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
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
        private InterpolateCharacterSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            InterpolateQuery(World, t);
        }

        [Query]
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
            Vector3 slopeModifier = ApplySlopeModifier.Execute(in settings, in rigidTransform, in movementInput, in jump, characterController, dt);

            ApplyVelocityStun.Execute(ref rigidTransform, in stunComponent);

            Vector3 movementDelta = rigidTransform.MoveVelocity.Velocity * dt;
            Vector3 gravityDelta = rigidTransform.GravityVelocity * dt;

            // In order for some systems to work correctly we move the character horizontally and then vertically
            CollisionFlags horizontalCollisionFlags = characterController.Move(movementDelta);
            CollisionFlags verticalCollisionFlags = characterController.Move(gravityDelta + slopeModifier);

            bool hasGroundedFlag = EnumUtils.HasFlag(verticalCollisionFlags, CollisionFlags.Below) || EnumUtils.HasFlag(horizontalCollisionFlags, CollisionFlags.Below);

            if (!Mathf.Approximately(gravityDelta.y, 0f))
                rigidTransform.IsGrounded = hasGroundedFlag || characterController.isGrounded;

            rigidTransform.IsCollidingWithWall = EnumUtils.HasFlag(horizontalCollisionFlags, CollisionFlags.Sides);

            // If we are on a platform we save our local position
            PlatformSaveLocalPosition.Execute(ref platformComponent, characterController.transform.position);
        }
    }
}
