using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Character.Components;
using ECS.Abstract;
using ECS.CharacterMotion.Components;
using ECS.CharacterMotion.Settings;
using ECS.Input;
using ECS.Input.Systems;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ECS.CharacterMotion.Systems
{
    [UpdateInGroup(typeof(InputGroup))]
    public partial class UpdateInputJumpSystem : UpdateInputSystem<JumpInputComponent, PlayerComponent>
    {
        private readonly InputAction inputAction;
        private SingleInstanceEntity fixedTick;

        public UpdateInputJumpSystem(World world, InputAction inputAction) : base(world)
        {
            this.inputAction = inputAction;
        }

        public override void Initialize()
        {
            base.Initialize();
            fixedTick = World.CachePhysicsTick();
        }

        protected override void Update(float t)
        {
            UpdateInputQuery(World, t, fixedTick.GetPhysicsTickComponent(World).Tick);
        }

        [Query]
        private void UpdateInput([Data] float t, [Data] int tickValue, ref JumpInputComponent inputToUpdate,
            ref CharacterRigidTransform characterPhysics, ref ICharacterControllerSettings characterControllerSettings)
        {
            if (characterPhysics.IsGrounded && inputAction.WasPressedThisFrame())
                inputToUpdate.IsChargingJump = true;
            else if (inputToUpdate.IsChargingJump)
            {
                inputToUpdate.CurrentHoldTime += t;

                if (inputAction.WasReleasedThisFrame() || inputToUpdate.CurrentHoldTime > characterControllerSettings.HoldJumpTime)
                {
                    // +1 because Update is executed before Physics so it will always hold the previous tick value
                    inputToUpdate.PhysicalButtonArguments.TickWhenJumpOccurred = tickValue + 1;

                    inputToUpdate.PhysicalButtonArguments.Power =
                        Mathf.Clamp01(inputToUpdate.CurrentHoldTime / characterControllerSettings.HoldJumpTime);

                    inputToUpdate.IsChargingJump = false;
                    inputToUpdate.CurrentHoldTime = 0;
                }
            }
        }
    }
}
