using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Character.CharacterMotion.Components;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Systems;
using DCL.Diagnostics;
using DCL.Input;
using DCL.McpServer.Components;
using DCL.McpServer.Core;
using ECS.Abstract;
using UnityEngine;

namespace DCL.McpServer.Systems
{
    /// <summary>
    ///     While an <see cref="McpEcsMovementOverride" /> is present on the player entity, re-asserts its axes into
    ///     <see cref="MovementInputComponent" /> after the real-input systems have written it, so an agent-requested
    ///     walk survives the per-frame overwrite performed by <see cref="UpdateInputMovementSystem" />.
    /// </summary>
    [UpdateInGroup(typeof(InputGroup))]
    [UpdateAfter(typeof(UpdateInputMovementSystem))]
    [UpdateAfter(typeof(UpdateInputJumpSystem))]
    [LogCategory(ReportCategory.MCP)]
    public partial class McpInputOverrideSystem : BaseUnityLoopSystem
    {
        private readonly Entity playerEntity;

        private SingleInstanceEntity physicsTick;

        internal McpInputOverrideSystem(World world, Entity playerEntity) : base(world)
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
            ref McpEcsMovementOverride ecsMovementOverride = ref World.TryGetRef<McpEcsMovementOverride>(playerEntity, out bool overrideExists);

            if (!overrideExists)
                return;

            ref MovementInputComponent movement = ref World.TryGetRef<MovementInputComponent>(playerEntity, out bool hasMovement);

            if (UnityEngine.Time.time < ecsMovementOverride.EndTime)
            {
                if (hasMovement)
                {
                    movement.Axes = ecsMovementOverride.Axes;
                    movement.Kind = ecsMovementOverride.Kind;
                }

                if (ecsMovementOverride.JumpRequested)
                {
                    ecsMovementOverride.JumpRequested = false;

                    ref JumpInputComponent jump = ref World.TryGetRef<JumpInputComponent>(playerEntity, out bool hasJump);

                    if (hasJump)
                        jump.Trigger.TickWhenJumpOccurred = physicsTick.GetPhysicsTickComponent(World).Tick + 1;
                }
            }
            else
            {
                if (hasMovement)
                {
                    movement.Axes = Vector2.zero;
                    movement.Kind = MovementKind.IDLE;
                }

                // The override is copied out before the structural removal; no component refs are touched afterwards.
                McpRequest.CompleteAndRemove(World, playerEntity, ecsMovementOverride, AsyncUnit.Default);
            }
        }
    }
}
