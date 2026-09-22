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
    ///     Delivers a <see cref="SyntheticPointerEventIntent" /> by posting a <see cref="SyntheticPointerInput" /> for the
    ///     reticle pipeline, then reads the outcome one frame later, before the next raycast overwrites it.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(PlayerOriginatedRaycastSystem))]
    [LogCategory(ReportCategory.SYNTHETIC_INPUT)]
    public partial class SyntheticPointerEventSystem : BaseUnityLoopSystem
    {
        private static readonly QueryDescription PIPELINE_ENTITY = new QueryDescription().WithAll<SyntheticPointerInput>();

        // Must outlast the longest button hold a driver can request.
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

            // Every system that writes the override installs it, so none depends on a sibling being registered.
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

        // On failure the request is completed and removed.
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

            // A scene reload keeps the parcel but recycles entity ids, so a press from the old world cannot be released.
            if (intent.Press.HasValue && !ReferenceEquals(world, intent.Press.Value.World))
            {
                CompleteAndRemove(in intent, Failure(in intent, "the scene reloaded mid-click"));
                return false;
            }

            sceneWorld = world;
            return true;
        }

        private void CompleteAndRemove(in SyntheticPointerEventIntent intent, SyntheticPointerResult result, SyntheticPressHandoff? press = null)
        {
            if (intent.Press.HasValue && intent.HasAimTarget && !result.Hit)
                result.UpRayMissed = true;

            // Read before the removal invalidates the ref.
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

                // The release must reach the scene on a later tick than the press. Re-posting the aim meanwhile keeps
                // the hover on the target.
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

            // Injected stays false until the hold ends, so the outcome is read only then.
            if (intent.IsHover && UnityEngine.Time.time < intent.HoldEndTime)
            {
                PostSyntheticInput(aimPoint);
                return;
            }

            PostSyntheticInput(aimPoint,
                intent.EventType == PointerEventType.PetDown ? intent.Button : null,
                intent.EventType == PointerEventType.PetUp ? intent.Button : null,
                targetEntity, sceneWorld);

            intent.Injected = true;
            intent.InjectedTick = scene.SceneStateProvider.TickNumber;
            intent.InjectedAimPoint = aimPoint;
        }

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
            // The pipeline clears the post when it consumes it. An unconsumed post is re-stamped so it does not go stale.
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

            // SyntheticAimPoint echoes only an aim the pipeline processed. An edge the pipeline skipped reached nobody.
            ref PlayerOriginRaycastResultForSceneEntities raycastResult = ref World.Get<PlayerOriginRaycastResultForSceneEntities>(pipelineEntity);
            bool pipelineProcessed = raycastResult.SyntheticAimPoint == intent.InjectedAimPoint;

            SyntheticPointerResult result = BuildResult(in intent, sceneWorld,
                in raycastResult,
                in World.Get<HoverStateComponent>(pipelineEntity),
                World.Get<HoverFeedbackComponent>(pipelineEntity).Tooltips,
                collidersGlobalCache,
                out SyntheticPressHandoff? press);

            // A missed untargeted press still reached the scene root, so it is handed off for the release leg.
            if (!result.Hit && pipelineProcessed && IsUntargeted(in intent))
            {
                result.RootBroadcast = true;

                if (intent.EventType == PointerEventType.PetDown)
                    press = new SyntheticPressHandoff { World = sceneWorld, Entity = Entity.Null, Tick = intent.InjectedTick };
            }

            Vector3? deliveredPress = null;

            // Keeps the hover on the target until the release leg.
            if (result.Hit && intent.EventType == PointerEventType.PetDown)
            {
                PostSyntheticInput(intent.InjectedAimPoint);
                deliveredPress = intent.InjectedAimPoint;
            }

            // The intent ref is invalid after CompleteAndRemove, hence the deliveredPress copy.
            CompleteAndRemove(in intent, result, press);

            if (deliveredPress is { } pressAimPoint)
                ParkPointerAtPress(pressAimPoint);
        }

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

        // An off-screen press parks nothing: its projected pixel is one the gesture never touched.
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

            // The cursor systems run in an earlier group, so without this the next frame falls back to the hardware mouse.
            World.Get<SyntheticCursorOverride>(playerCamera).AssertPointerPositionThisFrame(hold.ScreenPosition);
        }

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
