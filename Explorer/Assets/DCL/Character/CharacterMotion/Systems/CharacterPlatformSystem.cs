using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Platforms;
using DCL.CharacterMotion.Settings;
using DCL.Diagnostics;
using ECS.Abstract;
using UnityEngine;

namespace DCL.CharacterMotion.Systems
{
    [LogCategory(ReportCategory.MOTION)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(InterpolateCharacterSystem))]
    [UpdateBefore(typeof(RotateCharacterSystem))]
    public partial class CharacterPlatformSystem : BaseUnityLoopSystem
    {
        public CharacterPlatformSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            ResolvePlatformMovementQuery(World);
        }

        [Query]
        [None(typeof(PlayerTeleportIntent))]
        private void ResolvePlatformMovement(
            in ICharacterControllerSettings settings,
            ref CharacterPlatformComponent platformComponent,
            ref CharacterRigidTransform rigidTransform,
            ref CharacterController characterController)
        {
            rigidTransform.PlatformDelta = Vector3.zero;

            if (!rigidTransform.IsGrounded)
            {
                platformComponent.CurrentPlatform = null;
                return;
            }

            Transform transform = characterController.transform;

            PlatformRaycast.Execute(platformComponent, characterController.radius, transform, settings);

            if (platformComponent.CurrentPlatform == null) return;

            Transform platformTransform = platformComponent.CurrentPlatform.transform;

            Vector3 newGroundWorldPos = platformTransform.TransformPoint(platformComponent.LastPosition);
            Vector3 newCharacterForward = platformTransform.TransformDirection(platformComponent.LastRotation);

            rigidTransform.PlatformDelta = newGroundWorldPos - transform.position;
            transform.forward = newCharacterForward;
        }
    }
}
