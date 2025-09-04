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
using Vector2 = UnityEngine.Vector2;
using Vector3 = Decentraland.Common.Vector3;

namespace DCL.SDKComponents.PrimaryPointerInfo.Systems
{
    [UpdateInGroup(typeof(SyncedInitializationSystemGroup))]
    [LogCategory(ReportCategory.INPUT)]
    public partial class PrimaryPointerInfoSystem : BaseUnityLoopSystem
    {
        private readonly World globalWorld;
        private InputAction inputPoint;
        private readonly IECSToCRDTWriter ecsToCRDTWriter;
        private Vector2 previousPosition = Vector2.zero;
        private Vector2 deltaPos;
        private Vector3 worldRayDirection;
        private Camera cachedCamera;
        private readonly ISceneStateProvider sceneStateProvider;

        internal PrimaryPointerInfoSystem(
            World world,
            World globalWorld,
            ISceneStateProvider sceneStateProvider,
            IECSToCRDTWriter ecsToCRDTWriter
        ) : base(world)
        {
            this.globalWorld = globalWorld;
            this.sceneStateProvider = sceneStateProvider;
            this.ecsToCRDTWriter = ecsToCRDTWriter;
        }

        public override void Initialize()
        {
            base.Initialize();

            cachedCamera = globalWorld.CacheCamera().GetCameraComponent(globalWorld).Camera;

            inputPoint = DCLInput.Instance.Camera.Point;

            worldRayDirection = new Vector3();

            UpdatePointerInfo(Vector2.zero);
        }

        protected override void Update(float t)
        {
            if (!sceneStateProvider.IsCurrent) return;

            UpdatePointerInfo(inputPoint.ReadValue<Vector2>());
        }

        private void UpdatePointerInfo(Vector2 pointerPos)
        {
            deltaPos = pointerPos - previousPosition;
            previousPosition = pointerPos;

            var ray = cachedCamera.ScreenPointToRay(pointerPos);

            worldRayDirection.X = ray.direction.x;
            worldRayDirection.Y = ray.direction.y;
            worldRayDirection.Z = ray.direction.z;

            ecsToCRDTWriter.PutMessage<PBPrimaryPointerInfo, (Vector2 pos, Vector2 delta, Vector3 rayDir)>(static (component, data) =>
            {
                component.ScreenCoordinates.X = data.pos.x;
                component.ScreenCoordinates.Y = data.pos.y;

                component.ScreenDelta.X = data.delta.x;
                component.ScreenDelta.Y = data.delta.y;

                component.WorldRayDirection = data.rayDir;
            }, SpecialEntitiesID.SCENE_ROOT_ENTITY, (pointerPos, deltaPos, worldRayDirection));
        }
    }
}
