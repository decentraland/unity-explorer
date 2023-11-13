using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CrdtEcsBridge.Physics;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using DCL.Diagnostics;
using ECS.Abstract;
using System.Runtime.CompilerServices;
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
        private void ResolvePlatformMovement(
            in ICharacterControllerSettings settings,
            ref CharacterPlatformComponent platformComponent,
            ref CharacterRigidTransform rigidTransform,
            ref CharacterController characterController)
        {
            if (!rigidTransform.IsGrounded)
            {
                platformComponent.CurrentPlatform = null;
                return;
            }

            Transform transform = characterController.transform;

            CheckPlatform(platformComponent, transform, settings);

            if (platformComponent.CurrentPlatform == null) return;

            Transform platformTransform = platformComponent.CurrentPlatform.transform;

            Vector3 newGroundWorldPos = platformTransform.TransformPoint(platformComponent.LastPosition);
            Vector3 newCharacterForward = platformTransform.TransformDirection(platformComponent.LastRotation);

            Vector3 deltaPosition = newGroundWorldPos - transform.position;
            characterController.Move(deltaPosition);
            transform.forward = newCharacterForward;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CheckPlatform(CharacterPlatformComponent platformComponent, Transform transform, ICharacterControllerSettings settings)
        {
            float rayDistance = settings.PlatformRaycastLength;
            float halfDistance = rayDistance * 0.5f;

            var ray = new Ray
            {
                origin = transform.position + (Vector3.up * halfDistance),
                direction = Vector3.down,
            };

            bool hasHit = Physics.Raycast(ray, out RaycastHit hitInfo, rayDistance, PhysicsLayers.CHARACTER_ONLY_MASK);

            if (hasHit)
            {
                if (platformComponent.CurrentPlatform != hitInfo.collider.transform)
                {
                    platformComponent.CurrentPlatform = hitInfo.collider.transform;
                    platformComponent.LastPosition = platformComponent.CurrentPlatform.InverseTransformPoint(transform.position);
                    platformComponent.LastRotation = platformComponent.CurrentPlatform.InverseTransformDirection(transform.forward);
                }
            }
            else
                platformComponent.CurrentPlatform = null;
        }
    }
}
