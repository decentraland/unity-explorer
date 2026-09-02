using Arch.Core;
using Arch.SystemGroups;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.CharacterCamera;
using DCL.Diagnostics;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Groups;
using SceneRunner.Scene;
using UnityEngine;
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
        private Vector2 previousPosition = Vector2.zero;
        private CumulativePointerDelta lastSeenAccumulatedDelta;
        private Camera cachedCamera = null!;

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
            // The pointer the cursor pipeline resolved, never the mouse device directly: the device is not the
            // only thing that moves the pointer (a gamepad's virtual cursor, an automation gesture's injected
            // one), and reading an input action here also went blank whenever explorer UI took input focus and
            // disabled the Camera action map. The reticle ray is built from the same position, so the scene and
            // the client agree on where the pointer is.
            Vector2 cursorPosition = exposedCameraData.PointerScreenPosition;
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
                pointerPos = cursorPosition;
                deltaPos = cursorPosition - previousPosition;
            }

            // Always track the cursor position and the accumulated total, so the first frame after a
            // lock-state transition doesn't produce a stale-diff spike.
            previousPosition = cursorPosition;
            lastSeenAccumulatedDelta = accumulatedDelta;

            var ray = cachedCamera.ScreenPointToRay(pointerPos);

            var worldRayDirection = new Vector3
            {
                X = ray.direction.x,
                Y = ray.direction.y,
                Z = ray.direction.z,
            };

            ecsToCRDTWriter.PutMessage<PBPrimaryPointerInfo, (Vector2 pos, Vector2 delta, Vector3 rayDir)>(static (component, data) =>
            {
                component.PointerType = PointerType.PotMouse;
                component.ScreenCoordinates = new Decentraland.Common.Vector2 { X = data.pos.x, Y = data.pos.y };
                component.ScreenDelta = new Decentraland.Common.Vector2 { X = data.delta.x, Y = data.delta.y };
                component.WorldRayDirection = data.rayDir;
            }, SpecialEntitiesID.SCENE_ROOT_ENTITY, (pointerPos, deltaPos, worldRayDirection));
        }
    }
}
