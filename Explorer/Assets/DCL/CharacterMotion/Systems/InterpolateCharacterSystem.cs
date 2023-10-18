using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.CharacterCamera;
using DCL.CharacterMotion.Components;
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
            Vector3 delta = (rigidTransform.MoveVelocity.Target + rigidTransform.NonInterpolatedVelocity) * dt;

            CollisionFlags collisionFlags = characterController.Move(rigidTransform.MoveVelocity.Target * dt);

            bool hasGroundedFlag = EnumUtils.HasFlag(collisionFlags, CollisionFlags.Below);

            if (!Mathf.Approximately(delta.y, 0f))
                rigidTransform.IsGrounded = hasGroundedFlag;
        }

        private static float GetAcceleration(ICharacterControllerSettings characterControllerSettings, in CharacterRigidTransform physics) =>
            physics.IsGrounded ? characterControllerSettings.Acceleration : characterControllerSettings.AirAcceleration;
    }
}
