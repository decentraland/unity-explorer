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
        private void UpdateInput(ref MovementInputComponent inputToUpdate)
        {
            if (!movementAxis.enabled)
            {
                inputToUpdate.Axes = Vector2.zero;
                return;
            }

            inputToUpdate.Axes = movementAxis.ReadValue<Vector2>();

            if (autoWalkAction.WasPerformedThisFrame()) { inputToUpdate.AutoWalk = !inputToUpdate.AutoWalk; }

            if (inputToUpdate.Axes.sqrMagnitude > 0.1f) { inputToUpdate.AutoWalk = false; }

            // Running action wins over walking
            inputToUpdate.Kind = sprintAction.IsPressed() ? MovementKind.RUN :
                walkAction.IsPressed() ? MovementKind.WALK : MovementKind.JOG;

            if (inputToUpdate.Axes == Vector2.zero)
                inputToUpdate.Kind = MovementKind.IDLE;

            if (inputToUpdate.AutoWalk)
            {
                inputToUpdate.Axes = new Vector2(0, 1);
                inputToUpdate.Kind = MovementKind.WALK;
            }
        }
    }
}
