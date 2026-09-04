using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Character.CharacterCamera.Components;
using DCL.CharacterCamera;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Interaction.PlayerOriginated.Components;
using DCL.Interaction.Utility;
using DCL.SyntheticInput.Components;
using DCL.SyntheticInput.Core;
using DCL.SyntheticInput.UiSimulation;
using ECS.Abstract;
using ECS.SceneLifeCycle;
using ECS.Unity.PrimitiveColliders.Components;
using ECS.Unity.Transforms.Components;
using SceneRunner.Scene;
using UnityEngine;
using Utility.Arch;
using static DCL.SyntheticInput.Systems.SyntheticPointerDiagnostics;
using PlayerOriginatedRaycastSystem = DCL.Interaction.Systems.PlayerOriginatedRaycastSystem;

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
    ///         hover state, before the next raycast overwrites them (<see cref="SyntheticPointerDiagnostics" />
    ///         turns that state into the verdict).
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
    ///     <para>
    ///         Between an aimed press and its release the pointer itself is parked at the pixel the press landed
    ///         on (<see cref="SyntheticPointerHold" />), because the frames in between belong to no intent and a
    ///         driver has no hardware pointer of its own to leave there.
    ///     </para>
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(PlayerOriginatedRaycastSystem))]
    [LogCategory(ReportCategory.SYNTHETIC_INPUT)]
    public partial class SyntheticPointerEventSystem : BaseUnityLoopSystem
    {
        private static readonly QueryDescription ALL_ENTITIES = new ();
        private static readonly QueryDescription PIPELINE_ENTITY = new QueryDescription().WithAll<SyntheticPointerInput>();

        /// <summary>
        ///     How long a delivered press may keep the pointer parked with no release in sight. Covers the
        ///     longest hold a driver can ask for (press_input caps it at 30s) plus the driver-side completion
        ///     grace; past it an abandoned gesture hands the pointer back to the hardware mouse.
        /// </summary>
        private const float POINTER_HOLD_TIMEOUT_SEC = 35f;

        private readonly IScenesCache scenesCache;
        private readonly IEntityCollidersGlobalCache collidersGlobalCache;
        private readonly Entity playerEntity;
        private readonly UiCoverProbe? uiCoverProbe;

        private SingleInstanceEntity playerCamera;
        private SingleInstanceEntity pipelineEntity;

        internal SyntheticPointerEventSystem(World world,
            IScenesCache scenesCache,
            IEntityCollidersGlobalCache collidersGlobalCache,
            Entity playerEntity,
            UiCoverProbe? uiCoverProbe = null) : base(world)
        {
            this.scenesCache = scenesCache;
            this.collidersGlobalCache = collidersGlobalCache;
            this.playerEntity = playerEntity;
            this.uiCoverProbe = uiCoverProbe;
        }

        public override void Initialize()
        {
            base.Initialize();
            playerCamera = World.CacheCamera();
            pipelineEntity = new SingleInstanceEntity(in PIPELINE_ENTITY, World);

            // Installed once per session beside CursorComponent; the parked pointer only writes into it afterwards,
            // so no structural change happens while an intent ref is held.
            World.AddOrSet(playerCamera, SyntheticCursorOverride.Inactive);
        }

        protected override void Update(float t)
        {
            AssertHeldPointer();

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

            // Read before the removals below: every path that ends a release leg ends the hold with it, whether
            // the release was delivered or rejected — the driver is not holding the button any more either way.
            bool endsPointerHold = intent.EventType == PointerEventType.PetUp;

            EcsRequest.CompleteAndRemove(World, playerEntity, intent, new SyntheticPointerOutcome { Result = result, Press = press });

            if (endsPointerHold)
                World.TryRemove<SyntheticPointerHold>(playerEntity);
        }

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

            if (!TryResolveTargetEntity(in intent, sceneWorld, out Entity? targetEntity, out SyntheticPointerResult targetFailure))
            {
                CompleteAndRemove(in intent, targetFailure);
                return;
            }

            if (!TryResolveAimPoint(in intent, sceneWorld, targetEntity, out Vector3 aimPoint, out SyntheticPointerResult resolveFailure))
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

            // The edge carries the entity it was promised to: the pipeline withholds it from anything else its ray
            // selected, so a blocked or unqualified aim reports a miss having delivered no button anywhere.
            PostSyntheticInput(aimPoint,
                intent.EventType == PointerEventType.PetDown ? intent.Button : null,
                intent.EventType == PointerEventType.PetUp ? intent.Button : null,
                targetEntity, sceneWorld);

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

            Vector3? deliveredPress = null;

            // A press is usually followed by a release intent installed later this frame: hold the aim so the
            // hover does not leave the target in the gap between the two legs.
            if (result.Hit && intent.EventType == PointerEventType.PetDown)
            {
                PostSyntheticInput(intent.InjectedAimPoint);
                deliveredPress = intent.InjectedAimPoint;
            }

            // CompleteAndRemove invalidates the intent ref, so the pointer is parked from the copy above.
            CompleteAndRemove(in intent, result, press);

            if (deliveredPress is { } pressAimPoint)
                ParkPointerAtPress(pressAimPoint);
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
                result.HoverText = ResolveHoverText(in entityInfo, World.Get<HoverFeedbackComponent>(pipelineEntity).Tooltips);
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
                return DiagnoseMiss(in intent, raycastResult.OriginRay, collidersGlobalCache);

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
                    HoverText = ResolveHoverText(in entityInfo, World.Get<HoverFeedbackComponent>(pipelineEntity).Tooltips),
                    HitPoint = raycastResult.RaycastHit.point,
                    Distance = raycastResult.GetDistance(),
                };
            }

            return DiagnoseUnqualified(in intent, in entityInfo, hitEntity, hitCrdtId, raycastResult.GetDistance(),
                StoppedShortOfAim(in raycastResult, intent.InjectedAimPoint), raycastResult.Collider.name);
        }

        /// <summary>
        ///     Re-states the parked pointer position every frame a press is held. The cursor system takes the
        ///     pointer from <see cref="SyntheticCursorOverride" /> while it is asserted, which is what makes the
        ///     reticle ray — and the PBPrimaryPointerInfo ray built from the same position — follow the gesture
        ///     rather than the hardware mouse. A hold nobody released expires here.
        /// </summary>
        private void AssertHeldPointer()
        {
            if (!World.TryGet(playerEntity, out SyntheticPointerHold hold))
                return;

            if (UnityEngine.Time.time > hold.ExpiryTime)
            {
                World.Remove<SyntheticPointerHold>(playerEntity);
                return;
            }

            World.Get<SyntheticCursorOverride>(playerCamera).AssertPointerPositionThisFrame(hold.ScreenPosition);
        }

        /// <summary>
        ///     Parks the pointer at the pixel the delivered press occupies, for as long as the button stays down.
        ///     An aim that is not on screen is left alone: a driver can aim at a world point no human could have
        ///     clicked (behind the camera, out of the viewport), and a projection of it is a pixel the gesture
        ///     never touched.
        /// </summary>
        private void ParkPointerAtPress(Vector3 aimPoint)
        {
            Camera camera = playerCamera.GetCameraComponent(World).Camera;
            Vector3 projected = camera.WorldToScreenPoint(aimPoint);

            bool onScreen = projected.z > 0f
                            && projected.x >= 0f && projected.x <= camera.pixelWidth
                            && projected.y >= 0f && projected.y <= camera.pixelHeight;

            if (!onScreen)
                return;

            var hold = new SyntheticPointerHold
            {
                ScreenPosition = new Vector2(projected.x, projected.y),
                ExpiryTime = UnityEngine.Time.time + POINTER_HOLD_TIMEOUT_SEC,
            };

            World.AddOrSet(playerEntity, hold);

            // Stated for this frame too: the cursor systems run in an earlier group, so leaving it to the next
            // Update would hand the frame right after the press back to the hardware mouse.
            World.Get<SyntheticCursorOverride>(playerCamera).AssertPointerPositionThisFrame(hold.ScreenPosition);
        }

        /// <summary>
        ///     Posts the synthetic aim and/or button edge the pipeline consumes later this frame. A gesture that
        ///     named an entity passes it as <paramref name="targetEntity" />: only that entity may consume the
        ///     edge, and an edge no entity may consume is not broadcast to the scene root either. Null names no
        ///     entity, so an untargeted post stays a broadcast (Entity.Null is not default(Entity), which is why no
        ///     sentinel stands in for absence here).
        /// </summary>
        private void PostSyntheticInput(Vector3? aimPoint, InputAction? pressButton = null, InputAction? releaseButton = null,
            Entity? targetEntity = null, World? targetWorld = null)
        {
            World.Set(pipelineEntity, new SyntheticPointerInput
            {
                AimPoint = aimPoint,
                PressButton = pressButton,
                ReleaseButton = releaseButton,
                TargetEntity = targetEntity,
                TargetWorld = targetEntity.HasValue ? targetWorld : null,
                PostedAtFrame = UnityEngine.Time.frameCount,
            });
        }

        private static Vector3 ResolveAimPoint(in SyntheticPointerEventIntent intent, World sceneWorld, Entity targetEntity) =>
            intent.AimPoint ?? ResolveEntityAimPoint(sceneWorld, targetEntity);

        /// <summary>
        ///     The entity the gesture was promised, or null when it named none. Resolved before the aim, because it
        ///     is needed even when an explicit aim point makes the entity's own position irrelevant: it is the
        ///     entity the posted edge is restricted to, and a target id that resolves to nothing is a failure in
        ///     its own right rather than an aim that lands somewhere and reports a phantom blocker.
        /// </summary>
        private static bool TryResolveTargetEntity(in SyntheticPointerEventIntent intent, World sceneWorld, out Entity? targetEntity, out SyntheticPointerResult failure)
        {
            failure = default(SyntheticPointerResult);
            targetEntity = null;

            if (intent.Press is { } press)
            {
                // Liveness was checked before the release was ordered. An aimless press hands off Entity.Null:
                // its release names no entity either.
                if (press.Entity != Entity.Null)
                    targetEntity = press.Entity;

                return true;
            }

            if (intent.TargetEntityId < 0)
                return true;

            int targetId = intent.TargetEntityId;
            Entity found = Entity.Null;

            // TODO: resolve through CrdtEcsSynchronizer.EntitiesMap (O(1)) once MCP entity addressing moves from
            // raw Arch ids to CRDT ids, together with list_scene_entities/get_entity_details/WorldInfo, which scan
            // for the same reason.
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

            targetEntity = found;
            return true;
        }

        /// <summary>
        ///     Resolves the world point the synthetic ray must pass through. An explicit aim is taken as is and
        ///     needs no entity — the pipeline raycast still validates whatever the ray lands on; a screen-space
        ///     aim is projected to a far point along the camera ray through it. Otherwise the aim is the collider
        ///     center of <paramref name="targetEntity" />, already resolved by TryResolveTargetEntity.
        /// </summary>
        private bool TryResolveAimPoint(in SyntheticPointerEventIntent intent, World sceneWorld, Entity? targetEntity, out Vector3 aimPoint, out SyntheticPointerResult failure)
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
                // A screen-addressed aim names a pixel, so whatever owns that pixel intercepts it. The world-aim
                // path above deliberately keeps the pipeline's UI bypass: there the driver named a world target and
                // the cursor's position is irrelevant, but here a real click would never reach past the UI.
                if (!intent.Force && uiCoverProbe != null && uiCoverProbe(screenPoint, out string cover))
                {
                    failure = Failure(in intent, $"UI covers that point ({cover}); click the element with ui_click, or pass force to aim through it");
                    failure.BlockedByUi = cover;
                    return false;
                }

                Camera camera = playerCamera.GetCameraComponent(World).Camera;
                aimPoint = camera.ScreenPointToRay(screenPoint).GetPoint(PlayerOriginatedRaycastSystem.MAX_RAYCAST_DISTANCE);
                return true;
            }

            if (targetEntity is not { } target)
            {
                failure = Failure(in intent, "the gesture names neither an aim point nor a target entity");
                return false;
            }

            // A release aims at wherever its press target sits now, so the hover follows a target that moved
            // between the legs; a press aims at its own target's collider volume.
            aimPoint = ResolveEntityAimPoint(sceneWorld, target);
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
