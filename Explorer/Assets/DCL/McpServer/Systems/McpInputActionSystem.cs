using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Interaction.PlayerOriginated;
using DCL.Interaction.PlayerOriginated.Systems;
using DCL.McpServer.Components;
using DCL.McpServer.Core;
using ECS.Abstract;
using ECS.SceneLifeCycle;
using SceneRunner.Scene;
using System.Diagnostics.CodeAnalysis;

namespace DCL.McpServer.Systems
{
    /// <summary>
    ///     <para>
    ///         Delivers an agent-requested global input action while an <see cref="McpInputActionIntent" /> is
    ///         present on the player entity. The edge is published into the very <see cref="GlobalInputEvents" />
    ///         buffer the key bindings feed through <see cref="PrepareGlobalInputEventsSystem" />, so the current
    ///         scene's WritePointerEventResultsSystem turns it into an entity-less PBPointerEventsResult on the
    ///         scene root entity — the shape an SDK7 scene reads with inputSystem.isTriggered / isPressed when no
    ///         entity is involved. Nothing is raycast and no collider has to qualify: unlike
    ///         <see cref="McpPointerEventSystem" />, this path has no target.
    ///     </para>
    ///     <para>
    ///         The buffer is refilled from scratch every frame, so the entry must be added after
    ///         <see cref="PrepareGlobalInputEventsSystem" /> cleared it and before the scene worlds run their
    ///         PreRendering group later in the same frame. Publishing is as far as this system can see: the scene
    ///         writer drops the whole buffer for a frame in which an entity-targeted result was written instead
    ///         (the same suppression real input is subject to), so a request delivered concurrently with a
    ///         click_entity on a qualifying entity can be swallowed.
    ///     </para>
    ///     <para>
    ///         A press owns its release: the tool asks for a hold duration and the system publishes the PetUp
    ///         itself, so an agent that disconnects mid-hold cannot leave the scene believing the button is still
    ///         down. The release is withheld until the scene has advanced past the tick the press was stamped
    ///         with — the SDK keeps pointer results in a set keyed by that tick, and two edges sharing one key
    ///         collapse into an ambiguous button state.
    ///     </para>
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(PrepareGlobalInputEventsSystem))]
    [LogCategory(ReportCategory.MCP)]
    public partial class McpInputActionSystem : BaseUnityLoopSystem
    {
        private readonly IScenesCache scenesCache;
        private readonly GlobalInputEvents globalInputEvents;
        private readonly Entity playerEntity;

        internal McpInputActionSystem(World world,
            IScenesCache scenesCache,
            GlobalInputEvents globalInputEvents,
            Entity playerEntity) : base(world)
        {
            this.scenesCache = scenesCache;
            this.globalInputEvents = globalInputEvents;
            this.playerEntity = playerEntity;
        }

        protected override void Update(float t)
        {
            ref McpInputActionIntent intent = ref World.TryGetRef<McpInputActionIntent>(playerEntity, out bool exists);

            if (!exists)
                return;

            if (!TryResolve(in intent, out ISceneFacade? scene))
                return;

            if (intent.PressTime is { } pressTime)
                Release(ref intent, scene, pressTime);
            else
                Publish(ref intent, scene);
        }

        /// <summary>Picks the scene the edge must be delivered to, or completes the request with the reason no delivery is possible.</summary>
        private bool TryResolve(in McpInputActionIntent intent, [NotNullWhen(true)] out ISceneFacade? scene)
        {
            scene = scenesCache.CurrentScene.Value;

            if (scene == null || !scene.SceneStateProvider.IsCurrent || scene.SceneStateProvider.IsNotRunningState())
            {
                scene = null;
                Fail(in intent, "no running current scene to deliver the input action to");
                return false;
            }

            if (intent.SceneId != null && scene.SceneData.SceneEntityDefinition.id != intent.SceneId)
            {
                string reason = $"the request is pinned to scene '{intent.SceneId}' but the current scene is '{scene.Info.Name}' (did the player move?)";
                scene = null;
                Fail(in intent, reason);
                return false;
            }

            return true;
        }

        /// <summary>Publishes the requested edge; a press then waits out its hold, a lone edge completes here.</summary>
        private void Publish(ref McpInputActionIntent intent, ISceneFacade scene)
        {
            globalInputEvents.Add(new IGlobalInputEvents.Entry(intent.Action, intent.EventType));

            if (intent.HoldSeconds.HasValue)
            {
                intent.PressTime = UnityEngine.Time.time;
                return;
            }

            McpEcsRequest.CompleteAndRemove(World, playerEntity, intent, Delivered(scene));
        }

        /// <summary>Publishes the PetUp of a held press once both the hold and the tick gate have elapsed.</summary>
        private void Release(ref McpInputActionIntent intent, ISceneFacade scene, float pressTime)
        {
            // The scene stamps the press between the frame it was published on and this one, so the tick the
            // release is gated against is read a frame late: never too early, so the two edges cannot share it.
            // The capturing frame fails the gate below by construction, which is what buys that frame.
            intent.PressTick ??= scene.SceneStateProvider.TickNumber;

            float heldSeconds = UnityEngine.Time.time - pressTime;

            if (heldSeconds < intent.HoldSeconds || scene.SceneStateProvider.TickNumber <= intent.PressTick)
                return;

            globalInputEvents.Add(new IGlobalInputEvents.Entry(intent.Action, PointerEventType.PetUp));

            McpInputActionResult result = Delivered(scene);
            result.HeldSeconds = heldSeconds;
            McpEcsRequest.CompleteAndRemove(World, playerEntity, intent, result);
        }

        /// <summary>
        ///     Completes the request with the reason it could not be delivered. A press already published counts
        ///     as delivered whatever rejects the rest of it — only its release is lost, and the scene goes on
        ///     seeing the button held. The intent is copied out before the structural removal, so the caller's
        ///     ref must not be touched afterwards.
        /// </summary>
        private void Fail(in McpInputActionIntent intent, string reason)
        {
            bool pressed = intent.PressTime.HasValue;

            McpEcsRequest.CompleteAndRemove(World, playerEntity, intent, new McpInputActionResult
            {
                Delivered = pressed,
                ReleaseMissed = pressed,
                FailureReason = reason,
            });
        }

        private static McpInputActionResult Delivered(ISceneFacade scene) =>
            new ()
            {
                Delivered = true,
                SceneId = scene.SceneData.SceneEntityDefinition.id,
            };
    }
}
