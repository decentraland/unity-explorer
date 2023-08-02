using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.CharacterCamera;
using ECS.Abstract;
using ECS.CharacterMotion.Components;
using ECS.CharacterMotion.Settings;
using ECS.Input;
using ECS.Input.Systems.Physics;

namespace ECS.CharacterMotion.Systems
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
            ref CharacterPhysics physics,
            ref JumpInputComponent jump,
            ref CharacterRigidTransform rigidTransform,
            ref MovementInputComponent movementInput)
        {
            // Grounding should be calculated here?

            // Apply all velocities
            ApplyCharacterMovementVelocity.Execute(characterControllerSettings, ref physics, in camera, in movementInput, dt);
            ApplyJump.Execute(characterControllerSettings, ref jump, ref physics, physicsTick);
            ApplyGravity.Execute(characterControllerSettings, ref physics, dt);
            ApplyAirDrag.Execute(characterControllerSettings, ref physics, dt);

            // Calculate target position
            rigidTransform.PreviousTargetPosition = rigidTransform.TargetPosition;
            rigidTransform.TargetPosition += physics.Velocity * dt;
        }
    }
}
