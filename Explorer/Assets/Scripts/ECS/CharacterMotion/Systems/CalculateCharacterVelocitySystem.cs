using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CrdtEcsBridge.Components.Special;
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
        internal CalculateCharacterVelocitySystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            ResolveVelocityQuery(World, t);
        }

        [Query]
        private void ResolveVelocity(
            [Data] float dt,
            ref ICharacterControllerSettings characterControllerSettings,
            ref CharacterPhysics physics,
            ref CameraComponent camera,
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
