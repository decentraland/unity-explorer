using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.CharacterCamera;
using DCL.CharacterMotion.Components;
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
            ref CharacterRigidTransform rigidTransform,
            ref CharacterController characterController,
            ref CharacterPlatformComponent platformComponent)
        {
            Vector3 delta = (rigidTransform.MoveVelocity.Velocity + rigidTransform.NonInterpolatedVelocity) * dt;
            CollisionFlags collisionFlags = characterController.Move(delta);

            bool hasGroundedFlag = EnumUtils.HasFlag(collisionFlags, CollisionFlags.Below);

            if (!Mathf.Approximately(delta.y, 0f))
            {
                rigidTransform.IsGrounded = hasGroundedFlag || characterController.isGrounded;

                if (rigidTransform.IsGrounded)
                {
                    // I dont like this
                    rigidTransform.LastGroundedFrame = Mathf.CeilToInt(time.GetTimeComponent(World).Time / UnityEngine.Time.fixedDeltaTime);
                }
            }

            // TODO: Move this to other System?
            // We save our local position at the current platform
            if (platformComponent.CurrentPlatform != null)
                platformComponent.LastPosition = platformComponent.CurrentPlatform.transform.InverseTransformPoint(characterController.transform.position);
        }
    }
}
