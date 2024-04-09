using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Character.CharacterMotion.Components;
using DCL.Character.Components;
using DCL.CharacterMotion.Components;
using DCL.Input;
using DCL.Input.Systems;
using ECS.Abstract;
using UnityEngine.InputSystem;

namespace DCL.CharacterMotion.Systems
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
            UpdateInputQuery(World, fixedTick.GetPhysicsTickComponent(World).Tick);
        }

        [Query]
        [None(typeof(MovementBlockerComponent))]
        private void UpdateInput([Data] int tickValue, ref JumpInputComponent inputToUpdate)
        {
            if (inputAction.WasPressedThisFrame())
                inputToUpdate.Trigger.TickWhenJumpOccurred = tickValue + 1;

            inputToUpdate.IsPressed = inputAction.IsPressed();
        }
    }
}
