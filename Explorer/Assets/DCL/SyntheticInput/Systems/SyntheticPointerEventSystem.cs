using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CrdtEcsBridge.Physics;
using DCL.CharacterCamera;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Interaction.PlayerOriginated.Components;
using DCL.Interaction.Utility;
using DCL.SyntheticInput.Components;
using DCL.SyntheticInput.Core;
using ECS.Abstract;
using ECS.SceneLifeCycle;
using ECS.Unity.PrimitiveColliders.Components;
using ECS.Unity.Transforms.Components;
using SceneRunner.Scene;
using System.Collections.Generic;
using UnityEngine;
using PlayerOriginatedRaycastSystem = DCL.Interaction.Systems.PlayerOriginatedRaycastSystem;
using RaycastHit = UnityEngine.RaycastHit;

namespace DCL.SyntheticInput.Systems
{
    /// <summary>
    ///     <para>
    ///         Delivers a single agent-requested pointer event through the real reticle pipeline while a
    ///         <see cref="SyntheticPointerEventIntent" /> is present on the player entity. Instead of imitating the
    ///         pipeline, the system posts a <see cref="SyntheticPointerInput" /> (an aim point plus a button edge)
    ///         that <see cref="PlayerOriginatedRaycastSystem" /> and
    ///         <see cref="DCL.Interaction.Systems.ProcessPointerEventsSystem" /> consume the same frame, so
    ///         occlusion, distance gates, hover enter/leave and the scene write-back are all executed by the
    ///         production code. The outcome is read back one frame later from the pipeline's own raycast and
    ///         hover state, before the next raycast overwrites them.
    ///     </para>
    ///     <para>
    ///         A release that follows a press (<see cref="SyntheticPointerEventIntent.Press" />) is posted only once
    ///         the scene has advanced past the press tick, so the scene observes PetDown on an earlier tick than
    ///         PetUp; SyntheticInputAgent composes a full click from two such intents. While the release waits,
    ///         the aim is re-posted every frame so the hover does not leave the target mid-click.
    ///     </para>
    ///     <para>
    ///         Hover-only intents re-post the aim without a button until their hold expires, producing the same
    ///         hover enter/leave flow as a real cursor. Aimless intents post only the button edge: the cursor ray
    ///         stays in charge and the edge fans out entity-bound or globally exactly like a real key press.
    ///     </para>
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(PlayerOriginatedRaycastSystem))]
    [LogCategory(ReportCategory.SYNTHETIC_INPUT)]
    public partial class SyntheticPointerEventSystem : BaseUnityLoopSystem
    {
        private static readonly QueryDescription ALL_ENTITIES = new ();
        private static readonly QueryDescription PIPELINE_ENTITY = new QueryDescription().WithAll<SyntheticPointerInput>();

        private readonly IScenesCache scenesCache;
        private readonly IEntityCollidersGlobalCache collidersGlobalCache;
        private readonly Entity playerEntity;

        private SingleInstanceEntity playerCamera;
        private SingleInstanceEntity pipelineEntity;

        internal SyntheticPointerEventSystem(World world,
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
            ref SyntheticPointerEventIntent intent = ref World.TryGetRef<SyntheticPointerEventIntent>(playerEntity, out bool exists);

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
        private bool TryResolve(in SyntheticPointerEventIntent intent, ISceneFacade? scene, out World? sceneWorld)
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
        private void CompleteAndRemove(in SyntheticPointerEventIntent intent, SyntheticPointerResult result, SyntheticPressHandoff? press = null)
        {
            // Whatever rejected a release that follows a delivered press, the scene observed only the PetDown.
            // An aimless release has no target to miss, so the flag stays clear.
            if (intent.Press.HasValue && intent.HasAimTarget && !result.Hit)
                result.UpRayMissed = true;

            EcsRequest.CompleteAndRemove(World, playerEntity, intent, new SyntheticPointerOutcome { Result = result, Press = press });
        }

        private static SyntheticPointerResult Failure(in SyntheticPointerEventIntent intent, string reason) =>
            new ()
            {
                Hit = false,
                FailureReason = reason,
                SceneEntityId = intent.TargetEntityId,
            };

        /// <summary>Posts the synthetic aim and/or button edge the pipeline will consume later this frame.</summary>
        private void Inject(ref SyntheticPointerEventIntent intent, ISceneFacade scene, World sceneWorld)
        {
            if (intent.Press is { } press)
            {
                bool pressLandedOnEntity = press.Entity != Entity.Null;

                if (pressLandedOnEntity && !sceneWorld.IsAlive(press.Entity))
                {
                    CompleteAndRemove(in intent, Failure(in intent, "the target entity was destroyed mid-click"));
                    return;
                }

                // The scene must observe the press on an earlier tick than the release, otherwise ordering is
                // ambiguous; hold the aim meanwhile so the hover does not leave the target.
                if (scene.SceneStateProvider.TickNumber <= press.Tick)
                {
                    if (pressLandedOnEntity)
                        PostSyntheticInput(ResolveAimPoint(in intent, sceneWorld, press.Entity));

                    return;
                }
            }

            if (!intent.HasAimTarget)
            {
                InjectAimless(ref intent, scene);
                return;
            }

            if (!TryResolveAimPoint(in intent, sceneWorld, out Vector3 aimPoint, out SyntheticPointerResult resolveFailure))
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

            // The hover hold: keep the aim alive without observing; the outcome is read once the hold expires.
            if (intent.IsHover && UnityEngine.Time.time < intent.HoldEndTime)
            {
                PostSyntheticInput(aimPoint);
                return;
            }

            PostSyntheticInput(aimPoint,
                intent.EventType == PointerEventType.PetDown ? intent.Button : null,
                intent.EventType == PointerEventType.PetUp ? intent.Button : null);

            intent.Injected = true;
            intent.InjectedTick = scene.SceneStateProvider.TickNumber;
            intent.InjectedAimPoint = aimPoint;
        }

        /// <summary>
        ///     An aimless button edge keeps the cursor ray: the pipeline appends it entity-bound if a qualified
        ///     entity happens to be hovered, and PrepareGlobalInputEventsSystem fans it out to the scene root
        ///     otherwise — exactly the split a real key press goes through.
        /// </summary>
        private void InjectAimless(ref SyntheticPointerEventIntent intent, ISceneFacade scene)
        {
            PostSyntheticInput(null,
                intent.EventType == PointerEventType.PetDown ? intent.Button : null,
                intent.EventType == PointerEventType.PetUp ? intent.Button : null);

            intent.Injected = true;
            intent.InjectedTick = scene.SceneStateProvider.TickNumber;
            intent.InjectedAimPoint = Vector3.zero;
        }

        /// <summary>Reads the pipeline's answer for the injected frame and completes the request.</summary>
        private void Observe(ref SyntheticPointerEventIntent intent, World sceneWorld)
        {
            // The pipeline has not consumed the posted input yet (paused simulation?); the stamp is renewed
            // so the post stays valid until it does, and the driver-side timeout bounds the wait.
            ref SyntheticPointerInput pending = ref World.Get<SyntheticPointerInput>(pipelineEntity);

            if (pending.AimPoint.HasValue || pending.PressButton.HasValue || pending.ReleaseButton.HasValue)
            {
                pending.PostedAtFrame = UnityEngine.Time.frameCount;
                return;
            }

            if (!intent.HasAimTarget)
            {
                CompleteAimless(ref intent, sceneWorld);
                return;
            }

            SyntheticPointerResult result = BuildResult(in intent, sceneWorld,
                in World.Get<PlayerOriginRaycastResultForSceneEntities>(pipelineEntity),
                in World.Get<HoverStateComponent>(pipelineEntity),
                out SyntheticPressHandoff? press);

            // A press is usually followed by a release intent installed later this frame: hold the aim so the
            // hover does not leave the target in the gap between the two legs.
            if (result.Hit && intent.EventType == PointerEventType.PetDown)
                PostSyntheticInput(intent.InjectedAimPoint);

            CompleteAndRemove(in intent, result, press);
        }

        /// <summary>
        ///     An aimless button edge has no target to validate: it entered the pipeline the moment it was
        ///     consumed, so the result only reports, opportunistically, what the cursor ray was hovering.
        /// </summary>
        private void CompleteAimless(ref SyntheticPointerEventIntent intent, World sceneWorld)
        {
            SyntheticPressHandoff? press = null;

            if (intent.EventType == PointerEventType.PetDown)
                press = new SyntheticPressHandoff
                {
                    World = sceneWorld,
                    Entity = Entity.Null,
                    Tick = intent.InjectedTick,
                };

            var result = new SyntheticPointerResult
            {
                Hit = false,
                SceneEntityId = -1,
            };

            ref readonly PlayerOriginRaycastResultForSceneEntities raycastResult = ref World.Get<PlayerOriginRaycastResultForSceneEntities>(pipelineEntity);
            ref readonly HoverStateComponent hoverState = ref World.Get<HoverStateComponent>(pipelineEntity);

            if (raycastResult is { IsValidHit: true, EntityInfo: { } entityInfo }
                && hoverState.HasCollider && hoverState.LastHitCollider == raycastResult.Collider && hoverState.IsAtDistance)
            {
                // The edge also landed entity-bound on the hovered target (which suppresses the global broadcast
                // for that scene, exactly as a real key press would).
                result.Hit = true;
                result.SceneEntityId = entityInfo.ColliderSceneEntityInfo.EntityReference.Id;
                result.CrdtEntityId = entityInfo.ColliderSceneEntityInfo.SDKEntity.Id;
                result.HoverText = ResolveHoverText(in entityInfo);
                result.HitPoint = raycastResult.RaycastHit.point;
                result.Distance = raycastResult.GetDistance();
            }

            CompleteAndRemove(in intent, result, press);
        }

        private SyntheticPointerResult BuildResult(in SyntheticPointerEventIntent intent, World sceneWorld,
            in PlayerOriginRaycastResultForSceneEntities raycastResult, in HoverStateComponent hoverState,
            out SyntheticPressHandoff? press)
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
                SyntheticPointerResult blocked = Failure(in intent, "another collider blocks the line of sight to the target");
                blocked.BlockedByEntityId = hitEntity.Id;
                blocked.BlockedByCrdtId = hitCrdtId;
                blocked.BlockedByColliderName = raycastResult.Collider.name;
                return blocked;
            }

            if (hoverState.HasCollider && hoverState.LastHitCollider == raycastResult.Collider && hoverState.IsAtDistance)
            {
                if (intent.EventType == PointerEventType.PetDown)
                    press = new SyntheticPressHandoff
                    {
                        World = sceneWorld,
                        Entity = hitEntity,
                        Tick = intent.InjectedTick,
                    };

                return new SyntheticPointerResult
                {
                    Hit = true,
                    SceneEntityId = hitEntity.Id,
                    CrdtEntityId = hitCrdtId,
                    HoverText = ResolveHoverText(in entityInfo),
                    HitPoint = raycastResult.RaycastHit.point,
                    Distance = raycastResult.GetDistance(),
                };
            }

            return DiagnoseUnqualified(in intent, in entityInfo, hitEntity, hitCrdtId, raycastResult.GetDistance(),
                StoppedShortOfAim(in raycastResult, intent.InjectedAimPoint), raycastResult.Collider.name);
        }

        /// <summary>The pipeline hit nothing usable: a cold-path raycast tells whether the aim reaches any collider at all.</summary>
        private SyntheticPointerResult DiagnoseMiss(in SyntheticPointerEventIntent intent, in Ray originRay)
        {
            if (!Physics.Raycast(originRay, out RaycastHit hit, PlayerOriginatedRaycastSystem.MAX_RAYCAST_DISTANCE, PhysicsLayers.PLAYER_ORIGIN_RAYCAST_MASK))
                return Failure(in intent, "the ray from the camera hit nothing (target may lack a collider)");

            return collidersGlobalCache.TryGetSceneEntity(hit.collider, out _)
                ? Failure(in intent, "the reticle found no scene entity under the aim (transient scene state; retry)")
                : Failure(in intent, $"the ray hit a non-scene collider '{hit.collider.name}'");
        }

        /// <summary>
        ///     The ray reached an entity the gesture accepts as its target, but the pipeline did not qualify it for
        ///     cursor input. An entity without PointerEvents that the ray reached <em>before</em> the requested aim
        ///     point is an occluder, not the target — reported as a block so an aim-point gesture gets the same
        ///     blocker diagnostics an entity-addressed one does.
        /// </summary>
        private static SyntheticPointerResult DiagnoseUnqualified(in SyntheticPointerEventIntent intent, in GlobalColliderSceneEntityInfo entityInfo,
            Entity hitEntity, int hitCrdtId, float distance, bool stoppedShortOfAim, string colliderName)
        {
            SyntheticPointerResult result;

            if (!entityInfo.TryGetPointerEvents(out PBPointerEvents? pbPointerEvents) || pbPointerEvents == null)
            {
                if (stoppedShortOfAim)
                {
                    result = Failure(in intent, "another collider blocks the line of sight to the aim point");
                    result.BlockedByEntityId = hitEntity.Id;
                    result.BlockedByCrdtId = hitCrdtId;
                    result.BlockedByColliderName = colliderName;
                    result.Distance = distance;
                    return result;
                }

                result = Failure(in intent, $"entity {hitEntity.Id} has no PointerEvents component (not clickable)");
            }
            else
                result = Failure(in intent, HasCursorEntry(pbPointerEvents)
                    ? $"target is out of range for its pointer events (hit distance {distance:F2}m)"
                    : "the target's pointer events are proximity-type only and the player is out of proximity range");

            result.SceneEntityId = hitEntity.Id;
            result.CrdtEntityId = hitCrdtId;
            result.Distance = distance;
            return result;
        }

        /// <summary>
        ///     True when the ray was stopped by geometry closer than the point it was aimed through: the camera-origin
        ///     hit distance is the comparable one (the pipeline's own distance is measured from the player focus in
        ///     third person).
        /// </summary>
        private static bool StoppedShortOfAim(in PlayerOriginRaycastResultForSceneEntities raycastResult, Vector3 aimPoint)
        {
            const float TOLERANCE = 0.05f;

            return raycastResult.RaycastHit.distance < Vector3.Distance(raycastResult.OriginRay.origin, aimPoint) - TOLERANCE;
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
        private static bool IsExpectedTarget(in SyntheticPointerEventIntent intent, Entity hitEntity)
        {
            if (intent.Press is { } press)
                return hitEntity == press.Entity;

            return intent.TargetEntityId < 0 || hitEntity.Id == intent.TargetEntityId;
        }

        private void PostSyntheticInput(Vector3? aimPoint, InputAction? pressButton = null, InputAction? releaseButton = null)
        {
            World.Set(pipelineEntity, new SyntheticPointerInput
            {
                AimPoint = aimPoint,
                PressButton = pressButton,
                ReleaseButton = releaseButton,
                PostedAtFrame = UnityEngine.Time.frameCount,
            });
        }

        /// <summary>
        ///     The hover text a human would read on the target. The client's tooltip is preferred, but it only
        ///     exists for press/release entries (a hover-only entity shows no key prompt), so the target's own
        ///     PointerEvents text is the fallback — otherwise hover-only entities report no text at all.
        /// </summary>
        private string? ResolveHoverText(in GlobalColliderSceneEntityInfo entityInfo)
        {
            IReadOnlyList<HoverFeedbackComponent.Tooltip> tooltips = World.Get<HoverFeedbackComponent>(pipelineEntity).Tooltips;

            if (tooltips is { Count: > 0 })
                return tooltips[0].Text;

            if (!entityInfo.TryGetPointerEvents(out PBPointerEvents? pbPointerEvents) || pbPointerEvents == null)
                return null;

            for (var i = 0; i < pbPointerEvents.PointerEvents!.Count; i++)
            {
                PBPointerEvents.Types.Entry entry = pbPointerEvents.PointerEvents[i]!;

                if (entry.InteractionType == InteractionType.Cursor && entry.EventInfo is { HasHoverText: true } info && !string.IsNullOrEmpty(info.HoverText))
                    return info.HoverText;
            }

            return null;
        }

        private static Vector3 ResolveAimPoint(in SyntheticPointerEventIntent intent, World sceneWorld, Entity targetEntity) =>
            intent.AimPoint ?? ResolveEntityAimPoint(sceneWorld, targetEntity);

        /// <summary>
        ///     Resolves the world point the synthetic ray must pass through. An explicit aim is taken as is and
        ///     needs no entity — the pipeline raycast still validates whatever the ray lands on; a screen-space
        ///     aim is projected to a far point along the camera ray through it. Otherwise the aim is the target
        ///     entity's collider center, and only this case scans the scene world: the target id is a raw Arch id,
        ///     recovered the same way list_scene_entities/get_entity_details recover it.
        /// </summary>
        private bool TryResolveAimPoint(in SyntheticPointerEventIntent intent, World sceneWorld, out Vector3 aimPoint, out SyntheticPointerResult failure)
        {
            failure = default(SyntheticPointerResult);
            aimPoint = default(Vector3);

            if (intent.AimPoint is { } explicitAim)
            {
                aimPoint = explicitAim;
                return true;
            }

            if (intent.ScreenPoint is { } screenPoint)
            {
                Camera camera = playerCamera.GetCameraComponent(World).Camera;
                aimPoint = camera.ScreenPointToRay(screenPoint).GetPoint(PlayerOriginatedRaycastSystem.MAX_RAYCAST_DISTANCE);
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
