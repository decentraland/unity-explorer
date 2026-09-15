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
    ///     Delivers one <see cref="SyntheticPointerEventIntent" /> through the real reticle pipeline: it posts a
    ///     <see cref="SyntheticPointerInput" /> that <see cref="PlayerOriginatedRaycastSystem" /> and
    ///     <see cref="DCL.Interaction.Systems.ProcessPointerEventsSystem" /> consume the same frame, then reads the
    ///     outcome from their raycast and hover state one frame later, before the next raycast overwrites it.
    ///     Imitating the pipeline here instead would bypass occlusion, distance gates and hover enter/leave.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(PlayerOriginatedRaycastSystem))]
    [LogCategory(ReportCategory.SYNTHETIC_INPUT)]
    public partial class SyntheticPointerEventSystem : BaseUnityLoopSystem
    {
        private static readonly QueryDescription PIPELINE_ENTITY = new QueryDescription().WithAll<SyntheticPointerInput>();

        /// <summary>Covers the longest hold a driver can ask for (press_input caps it at 30s) plus the driver-side completion grace.</summary>
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

            // Every system that writes the override installs it here, so none depends on a sibling being registered.
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

        /// <summary>Picks the world to deliver to; completes and removes the request when no delivery is possible.</summary>
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

            // A mid-click reload swaps in a new world for the same parcel and recycles entity ids, so a release
            // whose press belongs to the disposed world can only be failed.
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
            if (intent.Press.HasValue && intent.HasAimTarget && !result.Hit)
                result.UpRayMissed = true;

            // Read before the removal below invalidates the ref.
            bool endsPointerHold = intent.EventType == PointerEventType.PetUp;

            EcsRequest.CompleteAndRemove(World, playerEntity, intent, new SyntheticPointerOutcome { Result = result, Press = press });

            if (endsPointerHold)
                World.TryRemove<SyntheticPointerHold>(playerEntity);
        }

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

                // The scene must observe the press on an earlier tick than the release, otherwise the ordering is
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

            // Hold without marking Injected, so the outcome is only read once the hold expires.
            if (intent.IsHover && UnityEngine.Time.time < intent.HoldEndTime)
            {
                PostSyntheticInput(aimPoint);
                return;
            }

            // The edge carries the entity it was promised to: the pipeline withholds it from anything else its ray
            // selected, so a blocked aim reports a miss having delivered no button anywhere.
            PostSyntheticInput(aimPoint,
                intent.EventType == PointerEventType.PetDown ? intent.Button : null,
                intent.EventType == PointerEventType.PetUp ? intent.Button : null,
                targetEntity, sceneWorld);

            intent.Injected = true;
            intent.InjectedTick = scene.SceneStateProvider.TickNumber;
            intent.InjectedAimPoint = aimPoint;
        }

        /// <summary>An aimless edge keeps the cursor ray: the pipeline routes it entity-bound or to the scene root, as a real key press is routed.</summary>
        private void InjectAimless(ref SyntheticPointerEventIntent intent, ISceneFacade scene)
        {
            PostSyntheticInput(null,
                intent.EventType == PointerEventType.PetDown ? intent.Button : null,
                intent.EventType == PointerEventType.PetUp ? intent.Button : null);

            intent.Injected = true;
            intent.InjectedTick = scene.SceneStateProvider.TickNumber;
            intent.InjectedAimPoint = Vector3.zero;
        }

        private void Observe(ref SyntheticPointerEventIntent intent, World sceneWorld)
        {
            // The pipeline has not consumed the posted input yet (paused simulation?); renew the stamp so the
            // post stays valid until it does.
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
            // echoes nothing, and an edge it never processed reached nobody.
            ref PlayerOriginRaycastResultForSceneEntities raycastResult = ref World.Get<PlayerOriginRaycastResultForSceneEntities>(pipelineEntity);
            bool pipelineProcessed = raycastResult.SyntheticAimPoint == intent.InjectedAimPoint;

            SyntheticPointerResult result = BuildResult(in intent, sceneWorld,
                in raycastResult,
                in World.Get<HoverStateComponent>(pipelineEntity),
                World.Get<HoverFeedbackComponent>(pipelineEntity).Tooltips,
                collidersGlobalCache,
                out SyntheticPressHandoff? press);

            // An untargeted edge no entity consumed reached the scene root. Hand off its release too, or a root
            // left holding the button follows the camera into every gesture made in the meantime.
            if (!result.Hit && pipelineProcessed && IsUntargeted(in intent))
            {
                result.RootBroadcast = true;

                if (intent.EventType == PointerEventType.PetDown)
                    press = new SyntheticPressHandoff { World = sceneWorld, Entity = Entity.Null, Tick = intent.InjectedTick };
            }

            Vector3? deliveredPress = null;

            // Hold the aim so the hover does not leave the target in the gap before the release leg.
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

        /// <summary>The verdict is whatever the cursor ray happened to be hovering when the edge was consumed.</summary>
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
        ///     Re-states the parked pointer every frame a press is held: the cursor system reads
        ///     <see cref="SyntheticCursorOverride" /> only while it is asserted, and that is what makes the reticle
        ///     ray follow the gesture rather than the hardware mouse.
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
        ///     Parks the pointer at the pixel the delivered press occupies. An off-screen aim is left alone: a
        ///     driver can aim at a world point no human could have clicked, and projecting it yields a pixel the
        ///     gesture never touched.
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

            // The cursor systems run in an earlier group, so leaving this to the next Update would hand the frame
            // right after the press back to the hardware mouse.
            World.Get<SyntheticCursorOverride>(playerCamera).AssertPointerPositionThisFrame(hold.ScreenPosition);
        }

        /// <summary>
        ///     Posts the aim and/or button edge the pipeline consumes later this frame. Only
        ///     <paramref name="targetEntity" /> may consume the edge, and an edge it cannot consume reaches nobody;
        ///     null names no entity and stays a broadcast. Entity.Null is not default(Entity), so absence cannot be
        ///     spelled with a sentinel here.
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
