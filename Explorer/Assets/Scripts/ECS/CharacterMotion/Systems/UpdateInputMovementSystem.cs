using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Character.Components;
using ECS.CharacterMotion.Components;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ECS.Input.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class UpdateInputMovementSystem : UpdateInputSystem<MovementInputComponent, PlayerComponent>
    {
        private readonly InputAction movementAxis;
        private readonly InputAction sprintAction;

        public UpdateInputMovementSystem(World world, DCLInput dclInput) : base(world)
        {
            movementAxis = dclInput.Player.Movement;
            sprintAction = dclInput.Player.Sprint;
        }

        protected override void Update(float t)
        {
            UpdateInputQuery(World);
        }

        [Query]
        private void UpdateInput(ref MovementInputComponent inputToUpdate)
        {
            inputToUpdate.Axes = movementAxis.ReadValue<Vector2>();
            inputToUpdate.Kind = sprintAction.IsPressed() ? MovementKind.Walk : MovementKind.Run;
        }
    }
}
