using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Character.CharacterMotion.Components;
using DCL.Character.Components;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using DCL.Input;
using DCL.Input.Systems;
using DCL.SDKComponents.InputModifier.Components;
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

        protected override void Update(float t) =>
            UpdateInputQuery(World, fixedTick.GetPhysicsTickComponent(World).Tick);

        [Query]
        private void UpdateInput([Data] int tickValue, ref JumpInputComponent inputToUpdate, in JumpState jumpState, in InputModifierComponent inputModifierComponent, in ICharacterControllerSettings settings)
        {
            if (!inputAction.enabled) return;

            if (inputModifierComponent.DisableJump && jumpState.JumpCount == 0) return;

            if (inputModifierComponent.DisableDoubleJump && jumpState.JumpCount > 0) return;

            if (inputModifierComponent.DisableGliding && jumpState.JumpCount > settings.AirJumpCount) return;

            if (inputAction.WasPressedThisFrame())
                inputToUpdate.Trigger.TickWhenJumpOccurred = tickValue + 1;

            inputToUpdate.IsPressed = inputAction.IsPressed();
        }
    }
}
