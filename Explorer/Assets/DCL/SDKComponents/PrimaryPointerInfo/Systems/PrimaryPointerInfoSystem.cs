using Arch.Core;
using Arch.SystemGroups;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.CharacterCamera;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Utilities;
using ECS.Abstract;
using ECS.Groups;
using SceneRunner.Scene;
using UnityEngine;
using InputAction = UnityEngine.InputSystem.InputAction;
using PointerType = DCL.ECSComponents.PointerType;
using Vector2 = UnityEngine.Vector2;
using Vector3 = Decentraland.Common.Vector3;

namespace DCL.SDKComponents.PrimaryPointerInfo.Systems
{
    [UpdateInGroup(typeof(SyncedInitializationSystemGroup))]
    [LogCategory(ReportCategory.INPUT)]
    public partial class PrimaryPointerInfoSystem : BaseUnityLoopSystem
    {
        private readonly World globalWorld;
        private readonly IECSToCRDTWriter ecsToCRDTWriter;
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly IExposedCameraData exposedCameraData;
        private InputAction inputPoint = null!;
        private Vector2 previousPosition = Vector2.zero;
        private CumulativePointerDelta lastSeenAccumulatedDelta;
        private Camera cachedCamera = null!;

        // Change-gating bookkeeping: the last payload actually sent to the scene, so an
        // identical re-send (walking straight, no mouse movement) can be skipped entirely.
        private Vector2 lastSentPos;
        private Vector2 lastSentDelta;
        private UnityEngine.Vector3 lastSentRayDir;
        private bool hasSent;

        internal PrimaryPointerInfoSystem(
            World world,
            World globalWorld,
            ISceneStateProvider sceneStateProvider,
            IECSToCRDTWriter ecsToCRDTWriter,
            IExposedCameraData exposedCameraData
        ) : base(world)
        {
            this.globalWorld = globalWorld;
            this.sceneStateProvider = sceneStateProvider;
            this.ecsToCRDTWriter = ecsToCRDTWriter;
            this.exposedCameraData = exposedCameraData;
        }

        public override void Initialize()
        {
            base.Initialize();

            cachedCamera = globalWorld.CacheCamera().GetCameraComponent(globalWorld).Camera;

            inputPoint = DCLInput.Instance.Camera.Point;
            lastSeenAccumulatedDelta = exposedCameraData.AccumulatedPointerDelta;

            UpdatePointerInfo();
        }

        protected override void Update(float t)
        {
            if (!sceneStateProvider.IsCurrent) return;

            UpdatePointerInfo();
        }

        private void UpdatePointerInfo()
        {
            Vector2 rawPosition = inputPoint.ReadValue<Vector2>();
            CumulativePointerDelta accumulatedDelta = exposedCameraData.AccumulatedPointerDelta;
            Vector2 pointerPos;
            Vector2 deltaPos;

            if (exposedCameraData.PointerIsLocked.Value)
            {
                // Unity freezes the absolute pointer position while the cursor is locked, so source the delta
                // from the per-render-frame accumulated total (this system is throttled to the scene tick, and
                // the raw delta action resets every render frame) and report screen-center coordinates.
                pointerPos = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                deltaPos = accumulatedDelta - lastSeenAccumulatedDelta;
            }
            else
            {
                pointerPos = rawPosition;
                deltaPos = rawPosition - previousPosition;
            }

            // Always track the raw position and the accumulated total, so the first frame after a
            // lock-state transition doesn't produce a stale-diff spike.
            previousPosition = rawPosition;
            lastSeenAccumulatedDelta = accumulatedDelta;

            Ray ray = cachedCamera.ScreenPointToRay(pointerPos);
            UnityEngine.Vector3 rayDirection = ray.direction;

            // PBPrimaryPointerInfo is last-writer-wins and this system is its sole writer for
            // SCENE_ROOT_ENTITY. ScreenCoordinates and WorldRayDirection are absolute, so an identical
            // re-send is a no-op; ScreenDelta is a per-tick quantity scenes integrate, so a non-zero delta
            // must be sent every tick it repeats (constant-velocity locked look) and its return to zero too.
            // Skip only the steady state: no motion continuing an already-zero delta with both absolute
            // fields unchanged. previousPosition / lastSeenAccumulatedDelta above update unconditionally, so
            // the skip never corrupts next tick's delta.
            if (hasSent
                && deltaPos.Equals(Vector2.zero)
                && lastSentDelta.Equals(Vector2.zero)
                && pointerPos.Equals(lastSentPos)
                && rayDirection.Equals(lastSentRayDir))
                return;

            var worldRayDirection = new Vector3
            {
                X = rayDirection.x,
                Y = rayDirection.y,
                Z = rayDirection.z,
            };

            ecsToCRDTWriter.PutMessage<PBPrimaryPointerInfo, (Vector2 pos, Vector2 delta, Vector3 rayDir)>(static (component, data) =>
            {
                component.PointerType = PointerType.PotMouse;
                component.ScreenCoordinates = new Decentraland.Common.Vector2 { X = data.pos.x, Y = data.pos.y };
                component.ScreenDelta = new Decentraland.Common.Vector2 { X = data.delta.x, Y = data.delta.y };
                component.WorldRayDirection = data.rayDir;
            }, SpecialEntitiesID.SCENE_ROOT_ENTITY, (pointerPos, deltaPos, worldRayDirection));

            // Record what was sent only after the write is issued: if PutMessage throws, the bookkeeping
            // stays at the last delivered payload so the update is retried next tick, not gated out forever.
            lastSentPos = pointerPos;
            lastSentDelta = deltaPos;
            lastSentRayDir = rayDirection;
            hasSent = true;
        }
    }
}
