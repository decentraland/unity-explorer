using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Arch.SystemGroups.Metadata;
using DCL.CharacterCamera;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using DCL.Input;
using DCL.Input.Systems;
using ECS.Abstract;
using System;

namespace DCL.CharacterMotion.Systems
{
    /// <summary>
    ///     Entry point to calculate everything that affects character's velocity
    /// </summary>
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [UpdateAfter(typeof(UpdateInputPhysicsTickSystem))]
    public partial class CalculateCharacterVelocitySystem : BaseUnityLoopSystem
    {
        private SingleInstanceEntity camera;
        private SingleInstanceEntity fixedTick;

        internal CalculateCharacterVelocitySystem(World world) : base(world) { }

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
            [Data] in CameraComponent camera,
            ref ICharacterControllerSettings characterControllerSettings,
            ref JumpInputComponent jump,
            ref CharacterRigidTransform rigidTransform,
            ref MovementInputComponent movementInput)
        {
            // Apply all velocities
            ApplyCharacterMovementVelocity.Execute(characterControllerSettings, ref rigidTransform.MoveVelocity, in camera, in movementInput);
            ApplyJump.Execute(characterControllerSettings, ref jump, ref rigidTransform, physicsTick);
            ApplyGravity.Execute(characterControllerSettings, ref rigidTransform, dt);
            ApplyAirDrag.Execute(characterControllerSettings, ref rigidTransform, dt);
        }
    }
}
