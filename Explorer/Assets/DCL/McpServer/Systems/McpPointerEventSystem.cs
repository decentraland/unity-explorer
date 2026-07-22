using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CrdtEcsBridge.Physics;
using DCL.CharacterCamera;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Interaction.PlayerOriginated.Components;
using DCL.Interaction.PlayerOriginated.Systems;
using DCL.Interaction.Utility;
using DCL.McpServer.Components;
using DCL.McpServer.Core;
using ECS.Abstract;
using ECS.SceneLifeCycle;
using ECS.Unity.PrimitiveColliders.Components;
using ECS.Unity.Transforms.Components;
using SceneRunner.Scene;
using System.Collections.Generic;
using UnityEngine;
using PlayerOriginatedRaycastSystem = DCL.Interaction.Systems.PlayerOriginatedRaycastSystem;
using RaycastHit = UnityEngine.RaycastHit;

namespace DCL.McpServer.Systems
{
    /// <summary>
    ///     <para>
    ///         Delivers a single agent-requested pointer event through the real reticle pipeline while a
    ///         <see cref="McpPointerEventIntent" /> is present on the player entity. Instead of imitating the
    ///         pipeline, the system posts a <see cref="SyntheticPointerInput" /> (an aim point plus a button edge)
    ///         that <see cref="PlayerOriginatedRaycastSystem" /> and
    ///         <see cref="DCL.Interaction.Systems.ProcessPointerEventsSystem" /> consume the same frame, so
    ///         occlusion, distance gates, hover enter/leave and the scene write-back are all executed by the
    ///         production code. The outcome is read back one frame later from the pipeline's own raycast and
    ///         hover state, before the next raycast overwrites them.
    ///     </para>
    ///     <para>
    ///         A release that follows a press (<see cref="McpPointerEventIntent.Press" />) is posted only once
    ///         the scene has advanced past the press tick, so the scene observes PetDown on an earlier tick than
    ///         PetUp; the click_entity tool composes a full click from two such intents. While the release waits,
    ///         the aim is re-posted every frame so the hover does not leave the target mid-click.
    ///     </para>
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(PlayerOriginatedRaycastSystem))]
    [LogCategory(ReportCategory.MCP)]
    public partial class McpPointerEventSystem : BaseUnityLoopSystem
    {
        private static readonly QueryDescription ALL_ENTITIES = new ();
        private static readonly QueryDescription PIPELINE_ENTITY = new QueryDescription().WithAll<SyntheticPointerInput>();

        private readonly IScenesCache scenesCache;
        private readonly IEntityCollidersGlobalCache collidersGlobalCache;
        private readonly Entity playerEntity;

        private SingleInstanceEntity playerCamera;
        private SingleInstanceEntity pipelineEntity;

        internal McpPointerEventSystem(World world,
            IScenesCache scenesCache,
            IEntityCollidersGlobalCache collidersGlobalCache,
            Entity playerEntity) : base(world)
        {
            this.scenesCache = scenesCache;
            this.collidersGlobalCache = collidersGlobalCache;
            this.playerEntity = playerEntity;
        }

        public override void Initialize()
        {
            base.Initialize();
            playerCamera = World.CacheCamera();
            pipelineEntity = new SingleInstanceEntity(in PIPELINE_ENTITY, World);
        }

        protected override void Update(float t)
        {
            ref McpPointerEventIntent intent = ref World.TryGetRef<McpPointerEventIntent>(playerEntity, out bool exists);

            if (!exists)
                return;

            ISceneFacade? scene = scenesCache.CurrentScene.Value;

            if (!TryResolve(in intent, scene, out World? sceneWorld))
                return;

            if (intent.Injected)
                Observe(ref intent, sceneWorld!);
            else
                Inject(ref intent, scene!, sceneWorld!);
        }

        /// <summary>Picks the world the pointer event must be delivered to, or completes the request with the reason no delivery is possible.</summary>
        private bool TryResolve(in McpPointerEventIntent intent, ISceneFacade? scene, out World? sceneWorld)
        {
            sceneWorld = null;

            if (scene == null || !scene.SceneStateProvider.IsCurrent || scene.SceneStateProvider.IsNotRunningState())
            {
                CompleteAndRemove(in intent, Failure(in intent, "no running current scene to deliver the pointer event to"));
                return false;
            }

            if (intent.SceneId != null && scene.SceneData.SceneEntityDefinition.id != intent.SceneId)
            {
                CompleteAndRemove(in intent, Failure(in intent, $"the request is pinned to scene '{intent.SceneId}' but the current scene is '{scene.Info.Name}' (did the player move?)"));
                return false;
            }

            World world = scene.EcsExecutor.World;

            // A mid-click reload swaps in a new world for the same parcel; the press handoff belongs to the
            // disposed one (entity ids get recycled), so the release can only be failed.
            if (intent.Press.HasValue && !ReferenceEquals(world, intent.Press.Value.World))
            {
                CompleteAndRemove(in intent, Failure(in intent, "the scene reloaded mid-click"));
                return false;
            }

            sceneWorld = world;
            return true;
        }

        /// <summary>The intent is copied out before the structural removal, so the caller's ref must not be touched afterwards.</summary>
        private void CompleteAndRemove(in McpPointerEventIntent intent, McpPointerClickResult result, McpPressHandoff? press = null)
        {
            // Whatever rejected a release that follows a delivered press, the scene observed only the PetDown.
            if (intent.Press.HasValue && !result.Hit)
                result.UpRayMissed = true;

            McpEcsRequest.CompleteAndRemove(World, playerEntity, intent, new McpPointerEventOutcome { Result = result, Press = press });
        }

        private static McpPointerClickResult Failure(in McpPointerEventIntent intent, string reason) =>
            new ()
            {
                Hit = false,
                FailureReason = reason,
                SceneEntityId = intent.TargetEntityId,
            };

        /// <summary>Posts the synthetic aim and button edge the pipeline will consume later this frame.</summary>
        private void Inject(ref McpPointerEventIntent intent, ISceneFacade scene, World sceneWorld)
        {
            if (intent.Press is { } press)
            {
                if (!sceneWorld.IsAlive(press.Entity))
                {
                    CompleteAndRemove(in intent, Failure(in intent, "the target entity was destroyed mid-click"));
                    return;
                }

                // The scene must observe the press on an earlier tick than the release, otherwise ordering is
                // ambiguous; hold the aim meanwhile so the hover does not leave the target.
                if (scene.SceneStateProvider.TickNumber <= press.Tick)
                {
                    PostSyntheticInput(ResolveAimPoint(in intent, sceneWorld, press.Entity));
                    return;
                }
            }

            if (!TryResolveAimPoint(in intent, sceneWorld, out Vector3 aimPoint, out McpPointerClickResult resolveFailure))
            {
                CompleteAndRemove(in intent, resolveFailure);
                return;
            }

            Vector3 cameraPosition = playerCamera.GetCameraComponent(World).Camera.transform.position;

            if ((aimPoint - cameraPosition).sqrMagnitude < SyntheticPointerInput.MIN_AIM_DISTANCE_SQR)
            {
                CompleteAndRemove(in intent, Failure(in intent, "the camera is on top of the aim point; move back and retry"));
                return;
            }

            PostSyntheticInput(aimPoint,
                intent.EventType == PointerEventType.PetDown ? intent.Button : null,
                intent.EventType == PointerEventType.PetUp ? intent.Button : null);

            intent.Injected = true;
            intent.InjectedTick = scene.SceneStateProvider.TickNumber;
            intent.InjectedAimPoint = aimPoint;
        }

        /// <summary>Reads the pipeline's answer for the injected frame and completes the request.</summary>
        private void Observe(ref McpPointerEventIntent intent, World sceneWorld)
        {
            // The pipeline has not consumed the posted input yet (paused simulation?); the stamp is renewed
            // so the post stays valid until it does, and the tool-side timeout bounds the wait.
            ref SyntheticPointerInput pending = ref World.Get<SyntheticPointerInput>(pipelineEntity);

            if (pending.AimPoint.HasValue)
            {
                pending.PostedAtFrame = UnityEngine.Time.frameCount;
                return;
            }

            McpPointerClickResult result = BuildResult(in intent, sceneWorld,
                in World.Get<PlayerOriginRaycastResultForSceneEntities>(pipelineEntity),
                in World.Get<HoverStateComponent>(pipelineEntity),
                out McpPressHandoff? press);

            // A press is usually followed by a release intent installed later this frame: hold the aim so the
            // hover does not leave the target in the gap between the two legs.
            if (result.Hit && intent.EventType == PointerEventType.PetDown)
                PostSyntheticInput(intent.InjectedAimPoint);

            CompleteAndRemove(in intent, result, press);
        }

        private McpPointerClickResult BuildResult(in McpPointerEventIntent intent, World sceneWorld,
            in PlayerOriginRaycastResultForSceneEntities raycastResult, in HoverStateComponent hoverState,
            out McpPressHandoff? press)
        {
            press = null;

            // The pipeline echoes the aim it consumed; anything else means the guarded frame ignored the input.
            if (raycastResult.SyntheticAimPoint != intent.InjectedAimPoint)
                return Failure(in intent, "the reticle pipeline did not process the synthetic aim (is the cursor panning or the in-world camera active?)");

            if (!raycastResult.IsValidHit)
                return DiagnoseMiss(in intent, raycastResult.OriginRay);

            GlobalColliderSceneEntityInfo entityInfo = raycastResult.EntityInfo!.Value;
            Entity hitEntity = entityInfo.ColliderSceneEntityInfo.EntityReference;
            int hitCrdtId = entityInfo.ColliderSceneEntityInfo.SDKEntity.Id;

            if (!ReferenceEquals(entityInfo.EcsExecutor.World, sceneWorld))
                return Failure(in intent, $"the ray landed on a collider of a different scene ('{raycastResult.Collider.name}')");

            if (!IsExpectedTarget(in intent, hitEntity))
            {
                McpPointerClickResult blocked = Failure(in intent, "another collider blocks the line of sight to the target");
                blocked.BlockedByEntityId = hitEntity.Id;
                blocked.BlockedByCrdtId = hitCrdtId;
                blocked.BlockedByColliderName = raycastResult.Collider.name;
                return blocked;
            }

            if (hoverState.HasCollider && hoverState.LastHitCollider == raycastResult.Collider && hoverState.IsAtDistance)
            {
                if (intent.EventType == PointerEventType.PetDown)
                    press = new McpPressHandoff
                    {
                        World = sceneWorld,
                        Entity = hitEntity,
                        Tick = intent.InjectedTick,
                    };

                return new McpPointerClickResult
                {
                    Hit = true,
                    SceneEntityId = hitEntity.Id,
                    CrdtEntityId = hitCrdtId,
                    HoverText = FirstTooltipText(),
                    HitPoint = raycastResult.RaycastHit.point,
                    Distance = raycastResult.GetDistance(),
                };
            }

            return DiagnoseUnqualified(in intent, in entityInfo, hitEntity, hitCrdtId, raycastResult.GetDistance());
        }

        /// <summary>The pipeline hit nothing usable: a cold-path raycast tells whether the aim reaches any collider at all.</summary>
        private McpPointerClickResult DiagnoseMiss(in McpPointerEventIntent intent, in Ray originRay)
        {
            if (!Physics.Raycast(originRay, out RaycastHit hit, PlayerOriginatedRaycastSystem.MAX_RAYCAST_DISTANCE, PhysicsLayers.PLAYER_ORIGIN_RAYCAST_MASK))
                return Failure(in intent, "the ray from the camera hit nothing (target may lack a collider)");

            return collidersGlobalCache.TryGetSceneEntity(hit.collider, out _)
                ? Failure(in intent, "the reticle found no scene entity under the aim (transient scene state; retry)")
                : Failure(in intent, $"the ray hit a non-scene collider '{hit.collider.name}'");
        }

        /// <summary>The ray reached the expected entity, but the pipeline did not qualify it for cursor input.</summary>
        private static McpPointerClickResult DiagnoseUnqualified(in McpPointerEventIntent intent, in GlobalColliderSceneEntityInfo entityInfo, Entity hitEntity, int hitCrdtId, float distance)
        {
            McpPointerClickResult result;

            if (!entityInfo.TryGetPointerEvents(out PBPointerEvents? pbPointerEvents) || pbPointerEvents == null)
                result = Failure(in intent, $"entity {hitEntity.Id} has no PointerEvents component (not clickable)");
            else
                result = Failure(in intent, HasCursorEntry(pbPointerEvents)
                    ? $"target is out of range for its pointer events (hit distance {distance:F2}m)"
                    : "the target's pointer events are proximity-type only and the player is out of proximity range");

            result.SceneEntityId = hitEntity.Id;
            result.CrdtEntityId = hitCrdtId;
            result.Distance = distance;
            return result;
        }

        private static bool HasCursorEntry(PBPointerEvents pbPointerEvents)
        {
            for (var i = 0; i < pbPointerEvents.PointerEvents!.Count; i++)
                if (pbPointerEvents.PointerEvents[i]!.InteractionType == InteractionType.Cursor)
                    return true;

            return false;
        }

        /// <summary>
        ///     The release must land on the entity that received the press; a lone event with an explicit target
        ///     must land on that target. A pure aim-point event accepts whatever the pipeline hit.
        /// </summary>
        private static bool IsExpectedTarget(in McpPointerEventIntent intent, Entity hitEntity)
        {
            if (intent.Press is { } press)
                return hitEntity == press.Entity;

            return intent.TargetEntityId < 0 || hitEntity.Id == intent.TargetEntityId;
        }

        private void PostSyntheticInput(Vector3 aimPoint, InputAction? pressButton = null, InputAction? releaseButton = null)
        {
            World.Set(pipelineEntity, new SyntheticPointerInput
            {
                AimPoint = aimPoint,
                PressButton = pressButton,
                ReleaseButton = releaseButton,
                PostedAtFrame = UnityEngine.Time.frameCount,
            });
        }

        private string? FirstTooltipText()
        {
            IReadOnlyList<HoverFeedbackComponent.Tooltip>? tooltips = World.Get<HoverFeedbackComponent>(pipelineEntity).Tooltips;
            return tooltips is { Count: > 0 } ? tooltips[0].Text : null;
        }

        private static Vector3 ResolveAimPoint(in McpPointerEventIntent intent, World sceneWorld, Entity targetEntity) =>
            intent.AimPoint ?? ResolveEntityAimPoint(sceneWorld, targetEntity);

        /// <summary>
        ///     Resolves the world point the synthetic ray must pass through. An explicit aim is taken as is and
        ///     needs no entity — the pipeline raycast still validates whatever the ray lands on. Otherwise the aim
        ///     is the target entity's collider center, and only this case scans the scene world: the target id is a
        ///     raw Arch id, recovered the same way list_scene_entities/get_entity_details recover it.
        /// </summary>
        private static bool TryResolveAimPoint(in McpPointerEventIntent intent, World sceneWorld, out Vector3 aimPoint, out McpPointerClickResult failure)
        {
            failure = default(McpPointerClickResult);
            aimPoint = default(Vector3);

            if (intent.AimPoint is { } explicitAim)
            {
                aimPoint = explicitAim;
                return true;
            }

            // The press already resolved its target (liveness checked before the release is ordered); the release
            // aims at wherever that entity sits now, so the hover follows a target that moved between the legs.
            if (intent.Press is { } press)
            {
                aimPoint = ResolveEntityAimPoint(sceneWorld, press.Entity);
                return true;
            }

            Entity found = Entity.Null;
            int targetId = intent.TargetEntityId;

            // TODO (Vit): drop this scan in a follow-up by switching MCP entity addressing from raw Arch ids to
            // CRDT ids and resolving through CrdtEcsSynchronizer.EntitiesMap (O(1)); done together with
            // list_scene_entities/get_entity_details/WorldInfo, which scan for the same reason.
            sceneWorld.Query(in ALL_ENTITIES, entity =>
            {
                if (entity.Id == targetId)
                    found = entity;
            });

            if (found == Entity.Null)
            {
                failure = Failure(in intent, $"no entity with id {targetId} in the current scene world");
                return false;
            }

            aimPoint = ResolveEntityAimPoint(sceneWorld, found);
            return true;
        }

        /// <summary>Aim at the collider volume when available; entity pivots can sit at hinges or bases and miss.</summary>
        private static Vector3 ResolveEntityAimPoint(World sceneWorld, Entity entity)
        {
            if (sceneWorld.TryGet(entity, out PrimitiveColliderComponent primitiveCollider) && primitiveCollider.Collider != null)
                return primitiveCollider.Collider.bounds.center;

            if (sceneWorld.TryGet(entity, out TransformComponent transformComponent) && transformComponent.Transform != null)
                return transformComponent.Transform.position;

            return Vector3.zero;
        }
    }
}
