using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.CharacterCamera;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using DCL.Input;
using DCL.Time.Systems;
using ECS.Abstract;
using UnityEngine;

namespace DCL.CharacterMotion.Systems
{
    /// <summary>
    ///     Entry point to calculate everything that affects character's velocity
    /// </summary>
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [UpdateAfter(typeof(UpdatePhysicsTickSystem))]
    public partial class CalculateCharacterVelocitySystem : BaseUnityLoopSystem
    {
        private SingleInstanceEntity camera;
        private SingleInstanceEntity fixedTick;

        public CalculateCharacterVelocitySystem(World world) : base(world) { }

        public override void Initialize()
        {
            camera = World.CacheCamera();
            fixedTick = World.CachePhysicsTick();
        }

        protected override void Update(float t)
        {
            ResolveVelocityQuery(World, t, fixedTick.GetPhysicsTickComponent(World).Tick, in camera.GetCameraComponent(World));
        }

        [Query]
        private void ResolveVelocity(
            [Data] float dt,
            [Data] int physicsTick,
            [Data] in CameraComponent cameraComponent,
            ref ICharacterControllerSettings characterControllerSettings,
            ref CharacterRigidTransform rigidTransform,
            ref CharacterController characterController,
            in JumpInputComponent jump,
            in MovementInputComponent movementInput)
        {
            // Apply all velocities
            ApplyCharacterMovementVelocity.Execute(characterControllerSettings, ref rigidTransform, in cameraComponent, in movementInput, dt);
            ApplyEdgeSlip.Execute(characterControllerSettings, ref rigidTransform, characterController);
            ApplyJump.Execute(characterControllerSettings, ref rigidTransform, in jump, in movementInput, physicsTick);
            ApplyGravity.Execute(characterControllerSettings, ref rigidTransform, in jump, physicsTick, dt);
            ApplyAirDrag.Execute(characterControllerSettings, ref rigidTransform, dt);
        }
    }
}
