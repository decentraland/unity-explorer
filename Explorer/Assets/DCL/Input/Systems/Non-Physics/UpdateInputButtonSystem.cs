using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Input.Component;
using UnityEngine.InputSystem;

namespace DCL.Input.Systems
{
    [UpdateInGroup(typeof(InputGroup))]
    public partial class UpdateInputButtonSystem<T, TQueryComponent> : UpdateInputSystem<T, TQueryComponent> where T: struct, IKeyComponent
    {
        protected readonly InputAction inputAction;

        internal UpdateInputButtonSystem(World world, InputAction dclInputAction) : base(world)
        {
            inputAction = dclInputAction;
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
