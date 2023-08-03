using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.CharacterCamera;
using Diagnostics.ReportsHandling;
using ECS.Abstract;
using ECS.CharacterMotion.Components;
using ECS.Prioritization.Systems;
using ECS.Unity.Transforms.Components;
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
    [UpdateBefore(typeof(CheckCameraQualifiedForRepartitioningSystem))]
    [UpdateBefore(typeof(CameraGroup))]
    public partial class InterpolateCharacterSystem : BaseUnityLoopSystem
    {
        internal InterpolateCharacterSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            InterpolateQuery(World, t);
        }

        [Query]
        private void Interpolate([Data] float dt, ref CharacterRigidTransform rigidTransform, ref TransformComponent transformComponent, ref CharacterController characterController)
        {
            // we assume that target position is set in Fixed Update but for foreign avatars it can be set after CRDT processing
            float timeAheadOfLastFixedUpdate = Time.time - rigidTransform.ModificationTimestamp;
            float normalizedTimeAhead = Mathf.Clamp(timeAheadOfLastFixedUpdate / dt, 0f, 1f);

            // interpolate between previous and target position
            var interpolatedPosition = Vector3.Lerp
                (rigidTransform.PreviousTargetPosition, rigidTransform.TargetPosition, normalizedTimeAhead);

            Vector3 delta = interpolatedPosition - transformComponent.Transform.localPosition;
            CollisionFlags collisionFlags = characterController.Move(delta);

            rigidTransform.PhysicsValues.IsGrounded = EnumUtils.HasFlag(collisionFlags, CollisionFlags.Below);
        }
    }
}
