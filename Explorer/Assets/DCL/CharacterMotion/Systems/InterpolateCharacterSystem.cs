using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.CharacterCamera;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Platforms;
using DCL.CharacterMotion.Settings;
using DCL.CharacterMotion.Settings;
using DCL.Diagnostics;
using Diagnostics.ReportsHandling;
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
        private SingleInstanceEntity fixedTick;
        private readonly SingleInstanceEntity time;

        internal InterpolateCharacterSystem(World world) : base(world)
        {
            time = world.CacheTime();
        }

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

            Vector3 movementDelta = (rigidTransform.MoveVelocity.Velocity + rigidTransform.NonInterpolatedVelocity) * dt;

            CollisionFlags collisionFlags = characterController.Move(movementDelta + slopeModifier);

            bool hasGroundedFlag = EnumUtils.HasFlag(collisionFlags, CollisionFlags.Below);

            if (!Mathf.Approximately(movementDelta.y, 0f))
            {
                rigidTransform.IsGrounded = hasGroundedFlag || characterController.isGrounded;

                if (rigidTransform.IsGrounded)
                    rigidTransform.LastGroundedFrame = Mathf.CeilToInt(time.GetTimeComponent(World).Time / UnityEngine.Time.fixedDeltaTime);
            }

            SaveLocalPosition.Execute(ref platformComponent, characterController.transform.position);
        }
    }
}
