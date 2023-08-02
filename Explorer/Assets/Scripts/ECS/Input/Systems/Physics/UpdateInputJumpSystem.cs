using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.CharacterMotion.Components;
using ECS.CharacterMotion.Settings;
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
            GetTickValueQuery(World);
        }

        [Query]
        private void GetTickValue(ref PhysicsTickComponent physicsTickComponent)
        {
            UpdateInputQuery(World, physicsTickComponent.tick);
        }

        [Query]
        private void UpdateInput([Data]int tickValue, ref JumpInputComponent inputToUpdate,
            ref CharacterPhysics characterPhysics, ref ICharacterControllerSettings characterControllerSettings)
        {
            if (characterPhysics.IsGrounded && inputAction.WasPressedThisFrame())
                inputToUpdate.IsChargingJump = true;
            else if (inputToUpdate.IsChargingJump && (inputAction.WasReleasedThisFrame()
                                                  || inputToUpdate.CurrentHoldTime > characterControllerSettings.HoldJumpTime))
            {
                inputToUpdate.PhysicalButtonArguments.tickWhenJumpOcurred = tickValue;

                inputToUpdate.IsChargingJump = false;
                inputToUpdate.CurrentHoldTime = 0;
                inputToUpdate.PhysicalButtonArguments.Power =
                    Mathf.Clamp01(inputToUpdate.CurrentHoldTime / characterControllerSettings.HoldJumpTime);
            }
        }
    }
}
