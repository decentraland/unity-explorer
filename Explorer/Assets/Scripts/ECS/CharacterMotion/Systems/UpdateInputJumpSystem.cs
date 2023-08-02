using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.CharacterMotion.Components;
using ECS.CharacterMotion.Settings;
using ECS.Input.Component;
using ECS.Input.Component.Physics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ECS.Input.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class UpdateInputJumpSystem : UpdateInputSystem<JumpInputComponent>
    {

        private readonly InputAction inputAction;
        public UpdateInputJumpSystem(World world, InputAction inputAction) : base(world)
        {
            this.inputAction = inputAction;
        }

        protected override void Update(float t)
        {
            GetTickValueQuery(World, t);
        }

        [Query]
        private void GetTickValue([Data] float t, ref PhysicsTickComponent physicsTickComponent)
        {
            UpdateInputQuery(World, t, physicsTickComponent.tick);
        }

        [Query]
        private void UpdateInput([Data] float t, [Data] int tickValue, ref JumpInputComponent inputToUpdate,
            ref CharacterPhysics characterPhysics, ref ICharacterControllerSettings characterControllerSettings)
        {
            if (characterPhysics.IsGrounded && inputAction.WasPressedThisFrame())
                inputToUpdate.IsChargingJump = true;
            else if (inputToUpdate.IsChargingJump)
            {
                inputToUpdate.CurrentHoldTime += t;

                if (inputAction.WasReleasedThisFrame() || inputToUpdate.CurrentHoldTime > characterControllerSettings.HoldJumpTime)
                {
                    inputToUpdate.PhysicalButtonArguments.tickWhenJumpOcurred = tickValue;

                    inputToUpdate.PhysicalButtonArguments.Power =
                        Mathf.Clamp01(inputToUpdate.CurrentHoldTime / characterControllerSettings.HoldJumpTime);

                    inputToUpdate.IsChargingJump = false;
                    inputToUpdate.CurrentHoldTime = 0;
                }
            }
        }
    }
}
