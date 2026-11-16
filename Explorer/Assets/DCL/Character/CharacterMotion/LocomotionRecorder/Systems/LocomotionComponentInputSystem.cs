using Arch.Core;
using Arch.SystemGroups;
using DCL.Character.CharacterMotion.Components;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using DCL.CharacterMotion.Systems;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.Input;
using DCL.SDKComponents.InputModifier.Components;
using ECS.Abstract;
using UnityEngine;

namespace DCL.CharacterMotion.LocomotionRecorder
{
    /// <summary>
    ///     The <see cref="LocomotionInputSeam.Component" /> seam: overwrites <see cref="MovementInputComponent" /> after
    ///     <see cref="UpdateInputMovementSystem" /> and <see cref="JumpInputComponent" /> after <see cref="UpdateInputJumpSystem" />
    ///     with the suite's keys, built the way those systems build them (digital axes normalised on diagonals like the WASD
    ///     composite, the same movement-kind resolution, the same jump gates and press trigger, and the scene input locks).
    ///     It bypasses the device, the DCLInput actions and the Player action map's enabled state. Always injected,
    ///     idle unless the running suite uses this seam.
    /// </summary>
    [UpdateInGroup(typeof(InputGroup))]
    [UpdateAfter(typeof(UpdateInputMovementSystem))]
    [UpdateAfter(typeof(UpdateInputJumpSystem))]
    [LogCategory(ReportCategory.MCP)]
    public partial class LocomotionComponentInputSystem : BaseUnityLoopSystem
    {
        private readonly LocomotionSuiteSession session;
        private readonly Entity playerEntity;

        private SingleInstanceEntity physicsTick;

        /// <summary>The jump key as last driven: a step holding it after one that did not is a press (the action's <c>WasPressedThisFrame</c>).</summary>
        private bool jumpHeld;

        internal LocomotionComponentInputSystem(World world, LocomotionSuiteSession session, Entity playerEntity) : base(world)
        {
            this.session = session;
            this.playerEntity = playerEntity;
        }

        public override void Initialize()
        {
            physicsTick = World.CachePhysicsTick();
        }

        protected override void Update(float t)
        {
            if (session.Options.InputSeam != LocomotionInputSeam.Component || !session.TryGetDrivenInput(out LocomotionInputStep step))
            {
                jumpHeld = false;
                return;
            }

            InputModifierComponent inputModifier = World.TryGet(playerEntity, out InputModifierComponent modifier) ? modifier : default(InputModifierComponent);

            UpdateMovement(in step, in inputModifier);
            UpdateJump(in step, in inputModifier);
        }

        private void UpdateMovement(in LocomotionInputStep step, in InputModifierComponent inputModifier)
        {
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

        /// <summary>
        ///     Mirrors <see cref="UpdateInputJumpSystem" />: a gated frame leaves the component untouched (a press in it is
        ///     lost, as a key press is); otherwise a press arms the trigger for the next fixed tick and the held state is written.
        /// </summary>
        private void UpdateJump(in LocomotionInputStep step, in InputModifierComponent inputModifier)
        {
            bool pressed = step.Jump && !jumpHeld;
            jumpHeld = step.Jump;

            ref JumpInputComponent jumpInput = ref World.TryGetRef<JumpInputComponent>(playerEntity, out bool hasJumpInput);
            ref JumpState jumpState = ref World.TryGetRef<JumpState>(playerEntity, out bool hasJumpState);

            if (!hasJumpInput || !hasJumpState || !World.TryGet(playerEntity, out ICharacterControllerSettings? settings) || settings == null)
                return;

            bool isNormalJump = jumpState.JumpCount == 0;
            bool disableDoubleJump = inputModifier.DisableDoubleJump || !FeaturesRegistry.Instance.IsEnabled(FeatureId.DoubleJump);
            bool disableGliding = inputModifier.DisableGliding || !FeaturesRegistry.Instance.IsEnabled(FeatureId.Gliding);

            if (inputModifier.DisableJump && isNormalJump)
                return;

            if (disableDoubleJump && disableGliding && !isNormalJump)
                return;

            jumpState.MaxAirJumpCount = disableDoubleJump ? 0 : settings.AirJumpCount;

            if (disableGliding && jumpState.JumpCount > jumpState.MaxAirJumpCount)
                return;

            if (pressed)
                jumpInput.Trigger.TickWhenJumpOccurred = physicsTick.GetPhysicsTickComponent(World).Tick + 1;

            jumpInput.IsPressed = step.Jump;
        }
    }
}
