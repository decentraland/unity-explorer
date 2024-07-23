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
        private const int UNGROUNDED_FRAMES = 3;
        public CharacterPlatformSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            ResolvePlatformMovementQuery(World, t);
        }

        [Query]
        [None(typeof(PlayerTeleportIntent))]
        private void ResolvePlatformMovement([Data] float t,
            in ICharacterControllerSettings settings,
            ref CharacterPlatformComponent platformComponent,
            ref CharacterRigidTransform rigidTransform,
            ref CharacterController characterController)
        {
            rigidTransform.PlatformDelta = Vector3.zero;

            if (rigidTransform.JustJumped)
            {
                platformComponent.CurrentPlatform = null;
                return;
            }

            if (!rigidTransform.IsGrounded)
            {
                platformComponent.FramesUngrounded++;

                if (platformComponent.FramesUngrounded > UNGROUNDED_FRAMES)
                    platformComponent.CurrentPlatform = null;

                Debug.Log("VVV [Null Platform] - Not Grounded");
                return;
            }

            platformComponent.FramesUngrounded = 0;

            Transform characterTransform = characterController.transform;

            // Physics.simulationMode = SimulationMode.Script;
            // Physics.Simulate(t);
            PlatformRaycast.Execute(platformComponent, characterController.radius, characterTransform, settings);
            // Physics.simulationMode = SimulationMode.FixedUpdte;

            if (platformComponent.CurrentPlatform == null)
            {
                // Debug.Log("VVV [Null Platform] - After raycast (No Hit?)");
                return;
            }

            Transform platformTransform = platformComponent.CurrentPlatform.transform;

            // if(platformComponent.LastPlatformPosition != null)
            //     rigidTransform.PlatformDelta = platformTransform.position - platformComponent.LastPlatformPosition.Value;
            // else
            {
                Vector3 newGroundWorldPos = platformTransform.TransformPoint(platformComponent.LastAvatarRelativePosition);
                rigidTransform.PlatformDelta = newGroundWorldPos - characterTransform.position;
            }

            Vector3 newCharacterForward = platformTransform.TransformDirection(platformComponent.LastAvatarRelativeRotation);
            Vector3 rotationDelta = newCharacterForward - characterTransform.forward;
            rigidTransform.LookDirection += rotationDelta;
            characterTransform.forward = newCharacterForward;
        }
    }
}
