using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.CharacterCamera;
using Diagnostics.ReportsHandling;
using ECS.Abstract;
using ECS.CharacterMotion.Components;
using ECS.CharacterMotion.Settings;
using UnityEngine;
using Utility;

namespace ECS.CharacterMotion.Systems
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
        internal InterpolateCharacterSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            InterpolateQuery(World, t);
        }

        [Query]
        private void Interpolate(
            [Data] float dt,
            ref ICharacterControllerSettings settings,
            ref CharacterRigidTransform rigidTransform,
            ref CharacterController characterController)
        {
            float acceleration = GetAcceleration(settings, in rigidTransform);
            rigidTransform.MoveVelocity.Interpolated = Vector3.MoveTowards(rigidTransform.MoveVelocity.Interpolated, rigidTransform.MoveVelocity.Target, acceleration * dt);

            Vector3 delta = (rigidTransform.MoveVelocity.Interpolated + rigidTransform.NonInterpolatedVelocity) * dt;

            CollisionFlags collisionFlags = characterController.Move(delta);

            rigidTransform.IsGrounded = EnumUtils.HasFlag(collisionFlags, CollisionFlags.Below);
        }

        private static float GetAcceleration(ICharacterControllerSettings characterControllerSettings, in CharacterRigidTransform physics) =>
            physics.IsGrounded ? characterControllerSettings.GroundAcceleration : characterControllerSettings.AirAcceleration;
    }
}
