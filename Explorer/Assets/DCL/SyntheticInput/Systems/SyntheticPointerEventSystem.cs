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
using SceneRunner.Scene;
using UnityEngine;
using Utility.Arch;
using static DCL.SyntheticInput.Systems.SyntheticPointerAim;
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

            Camera camera = playerCamera.GetCameraComponent(World).Camera;

            if (!TryResolveAimPoint(in intent, sceneWorld, targetEntity, camera, uiCoverProbe, out Vector3 aimPoint, out SyntheticPointerResult resolveFailure))
            {
                CompleteAndRemove(in intent, resolveFailure);
                return;
            }

            if ((aimPoint - camera.transform.position).sqrMagnitude < SyntheticPointerInput.MIN_AIM_DISTANCE_SQR)
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

            // The pipeline echoes the aim it consumed; a frame it guarded away (cursor panning, in-world camera)
            // echoes nothing, and an edge it never processed reached nobody — root included.
            bool pipelineProcessed = World.Get<PlayerOriginRaycastResultForSceneEntities>(pipelineEntity).SyntheticAimPoint == intent.InjectedAimPoint;

            SyntheticPointerResult result = BuildResult(in intent, sceneWorld,
                in World.Get<PlayerOriginRaycastResultForSceneEntities>(pipelineEntity),
                in World.Get<HoverStateComponent>(pipelineEntity),
                World.Get<HoverFeedbackComponent>(pipelineEntity).Tooltips,
                collidersGlobalCache,
                out SyntheticPressHandoff? press);

            // An untargeted edge no entity consumed is a broadcast: the scene root received it, exactly as it
            // receives a human's click on nothing. A press the root received hands off its release like the
            // aimless path does (Entity.Null: tick ordering only), so the driver can let go of what it pressed —
            // a root left holding a button follows the camera into every gesture made in the meantime.
            if (!result.Hit && pipelineProcessed && IsUntargeted(in intent))
            {
                result.RootBroadcast = true;

                if (intent.EventType == PointerEventType.PetDown)
                    press = new SyntheticPressHandoff { World = sceneWorld, Entity = Entity.Null, Tick = intent.InjectedTick };
            }

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
        ///     An aimless press hands off Entity.Null: its release is ordered by tick only. The verdict is whatever
        ///     the cursor ray happened to be hovering when the edge was consumed.
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

            SyntheticPointerResult result = BuildAimlessResult(
                in World.Get<PlayerOriginRaycastResultForSceneEntities>(pipelineEntity),
                in World.Get<HoverStateComponent>(pipelineEntity),
                World.Get<HoverFeedbackComponent>(pipelineEntity).Tooltips);

            CompleteAndRemove(in intent, result, press);
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

    }
}
