using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Character.CharacterMotion.Components;
using DCL.Character.Components;
using DCL.Input;
using DCL.Input.Systems;
using DCL.SDKComponents.InputModifier.Components;
using ECS.Abstract;
using UnityEngine.InputSystem;

namespace DCL.CharacterMotion.Systems
{
    [UpdateInGroup(typeof(InputGroup))]
    public partial class UpdateInputExternalForceSystem : UpdateInputSystem<ExternalForceInputComponent, PlayerComponent>
    {
        private readonly InputAction inputAction;

        public UpdateInputExternalForceSystem(World world, InputAction inputAction) : base(world)
        {
            this.inputAction = inputAction;
        }

        protected override void Update(float t)
        {
            UpdateInputQuery(World);
        }

        [Query]
        private void UpdateInput(ref ExternalForceInputComponent inputToUpdate, in InputModifierComponent inputModifierComponent)
        {
            if (!inputAction.enabled) return;

            inputToUpdate.IsPressed = inputAction.IsPressed();
        }
    }
}
