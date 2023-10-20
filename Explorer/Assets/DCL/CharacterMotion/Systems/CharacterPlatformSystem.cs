using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CrdtEcsBridge.Physics;
using DCL.CharacterMotion.Components;
using Diagnostics.ReportsHandling;
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
            ref CharacterPlatformComponent platformComponent,
            ref CharacterRigidTransform rigidTransform,
            ref CharacterController characterController)
        {
            if (!rigidTransform.IsGrounded)
            {
                platformComponent.CurrentPlatform = null;
                return;
            }

            CheckPlatform(platformComponent, characterController);

            if (platformComponent.CurrentPlatform == null) return;

            Transform platformTransform = platformComponent.CurrentPlatform.transform;

            Vector3 newGroundWorldPos = platformTransform.TransformPoint(platformComponent.LastPosition);
            Vector3 newCharacterForward = platformTransform.TransformDirection(platformComponent.LastRotation);

            Vector3 deltaPosition = newGroundWorldPos - characterController.transform.position;
            characterController.Move(deltaPosition);
            characterController.transform.forward = newCharacterForward;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CheckPlatform(CharacterPlatformComponent platformComponent, CharacterController characterController)
        {
            var ray = new Ray
            {
                origin = characterController.transform.position + (Vector3.up * 0.15f),
                direction = Vector3.down,
            };

            bool hasHit = Physics.Raycast(ray, out RaycastHit hitInfo, 0.30f, PhysicsLayers.PLAYER_ORIGIN_RAYCAST_MASK);

            if (hasHit)
            {
                if (platformComponent.CurrentPlatform != hitInfo.collider.transform)
                {
                    platformComponent.CurrentPlatform = hitInfo.collider.transform;

                    platformComponent.LastPosition =
                        platformComponent.CurrentPlatform.InverseTransformPoint(characterController.transform.position);

                    platformComponent.LastRotation =
                        platformComponent.CurrentPlatform.InverseTransformDirection(characterController.transform.forward);
                }
            }
            else
                platformComponent.CurrentPlatform = null;
        }
    }
}
