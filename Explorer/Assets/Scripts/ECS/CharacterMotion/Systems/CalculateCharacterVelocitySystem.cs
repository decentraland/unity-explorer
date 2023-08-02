using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.CharacterCamera;
using ECS.Abstract;
using ECS.CharacterMotion.Components;
using ECS.CharacterMotion.Settings;

namespace ECS.CharacterMotion.Systems
{
    /// <summary>
    ///     Entry point to calculate everything that affects character's velocity
    /// </summary>
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    public partial class CalculateCharacterVelocitySystem : BaseUnityLoopSystem
    {
        private SingleInstanceEntity camera;

        internal CalculateCharacterVelocitySystem(World world) : base(world) { }

        public override void Initialize()
        {
            camera = World.CacheCamera();
        }

        protected override void Update(float t)
        {
            ResolveVelocityQuery(World, in camera.GetCameraComponent(World), t);
        }

        [Query]
        private void ResolveVelocity(
            [Data] in CameraComponent camera,
            [Data] float dt,
            ref ICharacterControllerSettings characterControllerSettings,
            ref CharacterPhysics physics,
            ref JumpInputComponent jump,
            ref MovementInputComponent movementInput)
        {
            // Grounding should be calculated here?

            // Apply all velocities
            ApplyCharacterMovementVelocity.Execute(characterControllerSettings, ref physics, in camera, in movementInput, dt);
            ApplyJump.Execute(characterControllerSettings, ref jump, ref physics);
            ApplyGravity.Execute(characterControllerSettings, ref physics, dt);
            ApplyAirDrag.Execute(characterControllerSettings, ref physics, dt);
        }
    }
}
