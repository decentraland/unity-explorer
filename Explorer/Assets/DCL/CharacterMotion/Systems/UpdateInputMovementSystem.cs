using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Character.Components;
using DCL.CharacterMotion.Components;
using DCL.Input;
using DCL.Input.Systems;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DCL.CharacterMotion.Systems
{
    [UpdateInGroup(typeof(InputGroup))]
    public partial class UpdateInputMovementSystem : UpdateInputSystem<MovementInputComponent, PlayerComponent>
    {
        private readonly InputAction movementAxis;
        private readonly InputAction sprintAction;
        private readonly InputAction walkAction;

        public UpdateInputMovementSystem(World world, DCLInput dclInput) : base(world)
        {
            movementAxis = dclInput.Player.Movement;
            sprintAction = dclInput.Player.Sprint;
            walkAction = dclInput.Player.Walk;
        }

        protected override void Update(float t)
        {
            UpdateInputQuery(World);
        }

        [Query]
        private void UpdateInput(ref MovementInputComponent inputToUpdate)
        {
            inputToUpdate.Axes = movementAxis.ReadValue<Vector2>();

            // Running action wins over walking
            inputToUpdate.Kind = sprintAction.IsPressed() ? MovementKind.Run :
                walkAction.IsPressed() ? MovementKind.Walk : MovementKind.Jog;
        }
    }
}
