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
            if (inputModifierComponent is { DisableAll: true } or { DisableWalk: true, DisableJog: true, DisableRun: true })
            {
                inputToUpdate.Axes = Vector2.zero;
                return;
            }

            inputToUpdate.Axes = movementAxis.enabled ? movementAxis.ReadValue<Vector2>() : Vector2.zero;

            if (!inputModifierComponent.DisableWalk && autoWalkAction.WasPerformedThisFrame())
                inputToUpdate.AutoWalk = !inputToUpdate.AutoWalk;

            if (inputToUpdate is { AutoWalk: true, Axes: { sqrMagnitude: > 0.1f } })
                inputToUpdate.AutoWalk = false;

            // Running action wins over walking
            if (!inputModifierComponent.DisableRun && sprintAction.IsPressed())
                inputToUpdate.Kind = MovementKind.Run;
            else if (!inputModifierComponent.DisableWalk && walkAction.IsPressed())
                inputToUpdate.Kind = MovementKind.Walk;
            else if(!inputModifierComponent.DisableJog)
                inputToUpdate.Kind = MovementKind.Jog;

            if (inputToUpdate.AutoWalk)
            {
                inputToUpdate.Axes = new Vector2(0, 1);
                inputToUpdate.Kind = MovementKind.Walk;
            }
        }
    }
}
