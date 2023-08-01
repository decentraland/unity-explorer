using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.Input.Component;
using ECS.Input.Component.Physics;
using UnityEngine.InputSystem;

namespace ECS.Input.Systems.Physics
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class UpdateInputPhysicsButtonSystem<T> : UpdateInputSystem<T> where T : struct, PhysicalKeyComponent
    {
        private readonly InputAction inputAction;
        private int tickValue;

        public UpdateInputPhysicsButtonSystem(World world, InputAction inputAction) : base(world)
        {
            this.inputAction = inputAction;
        }

        protected override void Update(float t)
        {
            GetTickValueQuery(World);
            base.Update(t);
        }

        [Query]
        private void GetTickValue(ref PhysicsTickComponent physicsTickComponent)
        {
            tickValue = physicsTickComponent.tick;
        }

        [Query]
        protected override void UpdateInput(ref T component)
        {
            if (inputAction.WasPressedThisFrame())
                component.SetPhysicsTickKeyDown(tickValue);

            if (inputAction.WasReleasedThisFrame())
                component.SetPhysicsTickKeyUp(tickValue);

            component.SetPhysicsKeyPressed(inputAction.IsPressed());
        }

    }
}
