using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Character.Components;
using DCL.CharacterMotion.Components;
using DCL.Input;
using DCL.Input.Systems;
using DCL.SDKComponents.InputModifier.Components;
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
        private void UpdateInput(ref MovementInputComponent inputToUpdate, in InputModifierComponent inputModifierComponent)
        {
            if (!movementAxis.enabled || inputModifierComponent is { DisableAll: true } or { DisableWalk: true, DisableJog: true, DisableRun: true })
            {
                inputToUpdate.Axes = Vector2.zero;
                return;
            }

            inputToUpdate.Axes = movementAxis.ReadValue<Vector2>();

            if (!inputModifierComponent.DisableWalk && autoWalkAction.WasPerformedThisFrame())
                inputToUpdate.AutoWalk = !inputToUpdate.AutoWalk;

            if (inputToUpdate is { AutoWalk: true, Axes: { sqrMagnitude: > 0.1f } })
                inputToUpdate.AutoWalk = false;

            if (inputToUpdate.Axes == Vector2.zero)
                inputToUpdate.Kind = MovementKind.IDLE;
            else
            {
                bool runPressed = sprintAction.IsPressed();
                bool walkPressed = walkAction.IsPressed();

                inputToUpdate.Kind = ProcessInputMovementKind(inputModifierComponent, runPressed, walkPressed);
            }

            if (inputToUpdate.AutoWalk)
            {
                inputToUpdate.Axes = new Vector2(0, 1);
                inputToUpdate.Kind = MovementKind.WALK;
            }
        }

        private static MovementKind ProcessInputMovementKind(InputModifierComponent inputModifierComponent, bool runPressed, bool walkPressed)
        {
            // Running action wins over walking
            if (runPressed)
            {
                if (inputModifierComponent.DisableRun)
                    return inputModifierComponent.DisableJog ? MovementKind.WALK : MovementKind.JOG;

                return MovementKind.RUN;
            }

            if (walkPressed)
            {
                if (inputModifierComponent.DisableWalk)
                    return inputModifierComponent.DisableJog ? MovementKind.RUN : MovementKind.JOG;

                return MovementKind.WALK;
            }

            if (inputModifierComponent.DisableJog)
            {
                if (inputModifierComponent.DisableWalk)
                    return MovementKind.RUN;

                return MovementKind.WALK;
            }

            return MovementKind.JOG;
        }
    }
}
