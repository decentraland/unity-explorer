using Arch.Core;
using Arch.SystemGroups;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.CharacterCamera;
using DCL.Diagnostics;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Groups;
using UnityEngine.InputSystem;
using Vector2 = UnityEngine.Vector2;

namespace DCL.SDKComponents.PrimaryPointerInfo.Systems
{
    [UpdateInGroup(typeof(SyncedInitializationSystemGroup))]
    [LogCategory(ReportCategory.INPUT)]
    public partial class PrimaryPointerInfoSystem : BaseUnityLoopSystem
    {
        private readonly World globalWorld;
        private readonly IECSToCRDTWriter ecsToCRDTWriter;
        private readonly ExposedCameraData exposedCameraData;
        private Vector2 previousPosition = Vector2.zero;
        private Vector2 mousePos;
        private Vector2 deltaPos;

        internal PrimaryPointerInfoSystem(
            World world,
            World globalWorld,
            IECSToCRDTWriter ecsToCRDTWriter,
            ExposedCameraData exposedCameraData
        ) : base(world)
        {
            this.globalWorld = globalWorld;
            this.ecsToCRDTWriter = ecsToCRDTWriter;
            this.exposedCameraData = exposedCameraData;
        }

        public override void Initialize()
        {
            base.Initialize();

            UpdatePointerInfo();
        }

        protected override void Update(float t)
        {
            UpdatePointerInfo();
        }

        private void UpdatePointerInfo()
        {
            mousePos = Mouse.current.position.value;
            deltaPos = mousePos - previousPosition;
            previousPosition = mousePos;
            ref CameraComponent cameraComponent = ref globalWorld.Get<CameraComponent>(exposedCameraData.CameraEntityProxy.Object);

            var ray = cameraComponent.Camera.ScreenPointToRay(mousePos);

            var worldRayDirection = new Decentraland.Common.Vector3
            {
                X = ray.direction.x,
                Y = ray.direction.y,
                Z = ray.direction.z,
            };
            ecsToCRDTWriter.PutMessage<PBPrimaryPointerInfo, (Vector2 pos, Vector2 delta, Decentraland.Common.Vector3 rayDir)>(static (component, data) =>
            {
                component.ScreenCoordinates = new Decentraland.Common.Vector2 { X = data.pos.x, Y = data.pos.y };
                component.ScreenDelta = new Decentraland.Common.Vector2 { X = data.delta.x, Y = data.delta.y };
                component.WorldRayDirection = data.rayDir;
            }, SpecialEntitiesID.SCENE_ROOT_ENTITY, (mousePos, deltaPos,worldRayDirection));
        }
    }
}
