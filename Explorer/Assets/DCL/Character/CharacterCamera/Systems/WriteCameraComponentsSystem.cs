using Arch.Core;
using Arch.SystemGroups;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.Components.Transform;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.Utilities.Extensions;
using ECS.Abstract;
using ECS.Groups;
using ECS.Prioritization.Components;
using ECS.Unity.Transforms;
using SceneRunner.Scene;

namespace DCL.CharacterCamera.Systems
{
    /// <summary>
    ///     Runs in the Scene World and propagates camera data to the JavaScript scene
    /// </summary>
    [UpdateInGroup(typeof(SyncedPresentationSystemGroup))]
    public partial class WriteCameraComponentsSystem : BaseUnityLoopSystem
    {
        private readonly IECSToCRDTWriter ecsToCrdtWriter;
        private readonly IExposedCameraData exposedCameraData;
        private readonly ISceneData sceneData;
        private readonly IPartitionComponent scenePartition;
        private readonly byte propagationThreshold;
        private readonly IComponentPool<SDKTransform> sdkTransformPool;
        private readonly IComponentPool<PBMainCamera> mainCameraPool;
        private readonly Entity cameraEntity;

        internal WriteCameraComponentsSystem(World world, IECSToCRDTWriter ecsToCrdtWriter, IExposedCameraData exposedCameraData, ISceneData sceneData, IPartitionComponent scenePartition,
            byte propagationThreshold, IComponentPool<SDKTransform> sdkTransformPool, IComponentPool<PBMainCamera> mainCameraPool, Entity cameraEntity) : base(world)
        {
            this.ecsToCrdtWriter = ecsToCrdtWriter;
            this.exposedCameraData = exposedCameraData;
            this.sceneData = sceneData;
            this.propagationThreshold = propagationThreshold;
            this.sdkTransformPool = sdkTransformPool;
            this.cameraEntity = cameraEntity;
            this.mainCameraPool = mainCameraPool;
            this.scenePartition = scenePartition;
        }

        public override void Initialize()
        {
            // Set camera position for a newly created scene
            var sdkTransform = ExposedTransformUtils.Put(ecsToCrdtWriter, exposedCameraData, SpecialEntitiesID.CAMERA_ENTITY, sceneData.Geometry.BaseParcelPosition, false)
                .EnsureNotNull();

            if (!World.Has<SDKTransform>(cameraEntity))
            {
                var newComponent = sdkTransformPool.Get();
                newComponent.Position = sdkTransform.Position;
                newComponent.Rotation = sdkTransform.Rotation;
                newComponent.Scale = sdkTransform.Scale;

                World.Add(cameraEntity, newComponent);
            }

            // Initialize SDK Main Camera component
            // The instance used in PutMessage() will automatically return to the pool.
            PBMainCamera pbMainCameraForCRDT = mainCameraPool.Get();
            ecsToCrdtWriter.PutMessage<PBMainCamera, PBMainCamera>(
                static (dataToWrite, ecsInstance) =>
                {
                    dataToWrite.VirtualCameraEntity = ecsInstance.VirtualCameraEntity;
                },
                SpecialEntitiesID.CAMERA_ENTITY,
                pbMainCameraForCRDT);

            // Add for our world as well; The same properties set in PutMessage() have to be
            // initialized so that later CRDT synchronization detects both versions of the component as
            // the same, otherwise horrible duplicated component on same entity problems arise on builds
            PBMainCamera pbMainCameraForWorld = mainCameraPool.Get();
            pbMainCameraForWorld.VirtualCameraEntity = 0;
            World.Add(cameraEntity, pbMainCameraForWorld);

            PropagateCameraData(false);
        }

        protected override void Update(float t)
        {
            if (scenePartition.Bucket > propagationThreshold)
                return;

            ExposedTransformUtils.Put(ecsToCrdtWriter, exposedCameraData, SpecialEntitiesID.CAMERA_ENTITY, sceneData.Geometry.BaseParcelPosition, true);
            PropagateCameraData(true);
        }

        private void PropagateCameraData(bool checkIsDirty)
        {
            if (!checkIsDirty || exposedCameraData.CameraType.IsDirty)
                ecsToCrdtWriter.PutMessage<PBCameraMode, IExposedCameraData>(static (mode, data) => mode.Mode = data.CameraType, SpecialEntitiesID.CAMERA_ENTITY, exposedCameraData);

            if (!checkIsDirty || exposedCameraData.PointerIsLocked.IsDirty)
                ecsToCrdtWriter.PutMessage<PBPointerLock, IExposedCameraData>(static (pointerLock, data) => pointerLock.IsPointerLocked = data.PointerIsLocked, SpecialEntitiesID.CAMERA_ENTITY, exposedCameraData);
        }
    }
}
