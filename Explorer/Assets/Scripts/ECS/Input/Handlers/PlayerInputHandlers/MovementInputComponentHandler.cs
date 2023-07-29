using ECS.CharacterMotion.Components;
using ECS.Input.Handler;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ECS.CharacterMotion.InputHandlers
{
    public class MovementInputComponentHandler : InputComponentHandler<MovementInputComponent>
    {

        private readonly InputAction movementInputAction;
        private readonly InputAction sprintInputAction;

        public MovementInputComponentHandler(DCLInput dclInput)
        {
            movementInputAction = dclInput.Player.Movement;
            sprintInputAction = dclInput.Player.Sprint;
        }

        public void HandleInput(float t, ref MovementInputComponent component)
        {
            component.Axes = movementInputAction.ReadValue<Vector2>();
            component.Kind = sprintInputAction.IsPressed() ? MovementKind.Walk : MovementKind.Run;
        }
    }
}
