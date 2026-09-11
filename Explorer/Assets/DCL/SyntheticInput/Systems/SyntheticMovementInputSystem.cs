using Arch.Core;
using Arch.SystemGroups;
using DCL.Character.CharacterMotion.Components;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Systems;
using DCL.Diagnostics;
using DCL.Input;
using DCL.SDKComponents.InputModifier.Components;
using DCL.SyntheticInput.Components;
using DCL.SyntheticInput.Core;
using ECS.Abstract;
using UnityEngine;

namespace DCL.SyntheticInput.Systems
{
    /// <summary>
    ///     While a <see cref="SyntheticMovementIntent" /> is present on the player entity, re-asserts its axes into
    ///     <see cref="MovementInputComponent" /> after the real-input systems have written it, so an agent-requested
    ///     walk survives the per-frame overwrite performed by <see cref="UpdateInputMovementSystem" />.
    ///     Scene InputModifier locks apply exactly as they do to real input, unless the intent opts out.
    /// </summary>
    [UpdateInGroup(typeof(InputGroup))]
    [UpdateAfter(typeof(UpdateInputMovementSystem))]
    [UpdateAfter(typeof(UpdateInputJumpSystem))]
    [LogCategory(ReportCategory.SYNTHETIC_INPUT)]
    public partial class SyntheticMovementInputSystem : BaseUnityLoopSystem
    {
        private readonly Entity playerEntity;

        private SingleInstanceEntity physicsTick;

        internal SyntheticMovementInputSystem(World world, Entity playerEntity) : base(world)
        {
            this.playerEntity = playerEntity;
        }

        public override void Initialize()
        {
            base.Initialize();
            physicsTick = World.CachePhysicsTick();
        }

        protected override void Update(float t)
        {
            ref SyntheticMovementIntent movementIntent = ref World.TryGetRef<SyntheticMovementIntent>(playerEntity, out bool overrideExists);

            if (!overrideExists)
                return;

            ref MovementInputComponent movement = ref World.TryGetRef<MovementInputComponent>(playerEntity, out bool hasMovement);

            if (UnityEngine.Time.time < movementIntent.EndTime)
            {
                InputModifierComponent inputModifier = ResolveInputModifier(in movementIntent);

                if (hasMovement)
                {
                    // The same locks real input obeys (UpdateInputMovementSystem): a movement lock idles the
                    // hold, disabled kinds degrade through the shared fallback table.
                    if (inputModifier is { DisableAll: true } or { DisableWalk: true, DisableJog: true, DisableRun: true })
                    {
                        movement.Axes = Vector2.zero;
                        movement.Kind = MovementKind.Idle;
                    }
                    else
                    {
                        movement.Axes = movementIntent.Axes;

                        movement.Kind = UpdateInputMovementSystem.ProcessInputMovementKind(inputModifier,
                            runPressed: movementIntent.Kind == MovementKind.Run,
                            walkPressed: movementIntent.Kind == MovementKind.Walk);
                    }
                }

                if (movementIntent.JumpRequested)
                {
                    movementIntent.JumpRequested = false;

                    ref JumpInputComponent jump = ref World.TryGetRef<JumpInputComponent>(playerEntity, out bool hasJump);

                    if (hasJump && !inputModifier.DisableJump)
                        jump.Trigger.TickWhenJumpOccurred = physicsTick.GetPhysicsTickComponent(World).Tick + 1;
                }
            }
            else
            {
                if (hasMovement)
                {
                    movement.Axes = Vector2.zero;
                    movement.Kind = MovementKind.Idle;
                }

                // The intent is copied out before the structural removal; no component refs are touched afterwards.
                EcsRequest.CompleteAndRemove(World, playerEntity, movementIntent, SyntheticInputDelivery.Completed);
            }
        }

        private InputModifierComponent ResolveInputModifier(in SyntheticMovementIntent movementIntent)
        {
            if (movementIntent.IgnoreInputModifiers)
                return default(InputModifierComponent);

            return World.TryGet(playerEntity, out InputModifierComponent inputModifier) ? inputModifier : default(InputModifierComponent);
        }
    }
}
