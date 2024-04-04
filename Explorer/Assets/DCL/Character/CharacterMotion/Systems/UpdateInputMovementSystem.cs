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
        private readonly InputAction autoWalkAction;

        public UpdateInputMovementSystem(World world, DCLInput dclInput) : base(world)
        {
            movementAxis = dclInput.Player.Movement;
            sprintAction = dclInput.Player.Sprint;
            walkAction = dclInput.Player.Walk;
            autoWalkAction = dclInput.Player.AutoWalk;
        }

        protected override void Update(float t)
        {
            UpdateInputQuery(World);
        }

        [Query]
        [None(typeof(MovementBlockerComponent))]
        private void UpdateInput(ref MovementInputComponent inputToUpdate)
        {
            inputToUpdate.Axes = movementAxis.ReadValue<Vector2>();

            if (!movementAxis.enabled)
                inputToUpdate.Axes = Vector2.zero;

            if (autoWalkAction.WasPerformedThisFrame()) { inputToUpdate.AutoWalk = !inputToUpdate.AutoWalk; }

            if (inputToUpdate.Axes.sqrMagnitude > 0.1f) { inputToUpdate.AutoWalk = false; }

            // Running action wins over walking
            inputToUpdate.Kind = sprintAction.IsPressed() ? MovementKind.Run :
                walkAction.IsPressed() ? MovementKind.Walk : MovementKind.Jog;

            if (inputToUpdate.AutoWalk)
            {
                inputToUpdate.Axes = new Vector2(0, 1);
                inputToUpdate.Kind = MovementKind.Walk;
            }
        }
    }
}
