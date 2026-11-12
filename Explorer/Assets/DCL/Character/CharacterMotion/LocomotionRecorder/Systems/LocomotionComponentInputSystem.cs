using Arch.Core;
using Arch.SystemGroups;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Systems;
using DCL.Diagnostics;
using DCL.Input;
using DCL.SDKComponents.InputModifier.Components;
using ECS.Abstract;
using UnityEngine;

namespace DCL.CharacterMotion.LocomotionRecorder
{
    /// <summary>
    ///     The <see cref="LocomotionInputSeam.Component" /> seam: overwrites <see cref="MovementInputComponent" /> after
    ///     <see cref="UpdateInputMovementSystem" /> with the suite's keys, built the way that system builds them (digital
    ///     axes normalised on diagonals like the WASD composite, the same movement-kind resolution and scene input locks).
    ///     It bypasses the device, the DCLInput actions and the Player action map's enabled state. Always injected,
    ///     idle unless the running suite uses this seam.
    /// </summary>
    [UpdateInGroup(typeof(InputGroup))]
    [UpdateAfter(typeof(UpdateInputMovementSystem))]
    [LogCategory(ReportCategory.MCP)]
    public partial class LocomotionComponentInputSystem : BaseUnityLoopSystem
    {
        private readonly LocomotionSuiteSession session;
        private readonly Entity playerEntity;

        internal LocomotionComponentInputSystem(World world, LocomotionSuiteSession session, Entity playerEntity) : base(world)
        {
            this.session = session;
            this.playerEntity = playerEntity;
        }

        protected override void Update(float t)
        {
            if (session.Options.InputSeam != LocomotionInputSeam.Component || !session.TryGetDrivenInput(out LocomotionInputStep step))
                return;

            InputModifierComponent inputModifier = World.TryGet(playerEntity, out InputModifierComponent modifier) ? modifier : default(InputModifierComponent);
            ref MovementInputComponent movement = ref World.TryGetRef<MovementInputComponent>(playerEntity, out bool hasMovement);

            if (!hasMovement)
                return;

            var axes = new Vector2(step.Right, step.Fwd);

            if (axes == Vector2.zero || inputModifier is { DisableAll: true } or { DisableWalk: true, DisableJog: true, DisableRun: true })
            {
                movement.Axes = Vector2.zero;
                movement.Kind = MovementKind.Idle;
                return;
            }

            movement.Axes = axes.normalized;
            movement.Kind = UpdateInputMovementSystem.ProcessInputMovementKind(inputModifier, step.Sprint, step.Walk);
        }
    }
}
