using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.Input.Component;
using System;
using UnityEngine.InputSystem;

namespace ECS.Input.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class UpdateInputButtonSystem<T> : UpdateInputSystem<T> where T : struct, KeyComponent
    {

        protected readonly InputAction inputAction;

        public UpdateInputButtonSystem(World world, InputAction dclInputAction) : base(world)
        {
            this.inputAction = dclInputAction;
        }

        protected override void Update(float t)
        {
            UpdateInputQuery(World);
        }

        [Query]
        private void UpdateInput(ref T component)
        {
            component.SetKeyDown(inputAction.WasPressedThisFrame());
            component.SetKeyUp(inputAction.WasReleasedThisFrame());
            component.SetKeyPressed(inputAction.IsPressed());
        }

    }
}
