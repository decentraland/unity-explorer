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
        private readonly Entity cameraEntity;
        


        internal WriteCameraComponentsSystem(World world, IECSToCRDTWriter ecsToCrdtWriter, IExposedCameraData exposedCameraData, ISceneData sceneData, IPartitionComponent scenePartition,
            byte propagationThreshold, IComponentPool<SDKTransform> sdkTransformPool, Entity cameraEntity) : base(world)
        {
            this.ecsToCrdtWriter = ecsToCrdtWriter;
            this.exposedCameraData = exposedCameraData;
            this.sceneData = sceneData;
            this.propagationThreshold = propagationThreshold;
            this.sdkTransformPool = sdkTransformPool;
            this.cameraEntity = cameraEntity;
            this.scenePartition = scenePartition;
        }

        public override void Initialize()
        {
            // set camera position for a newly created scene
            var sdkTransform = ExposedTransformUtils.Put(ecsToCrdtWriter, exposedCameraData, SpecialEntitiesID.CAMERA_ENTITY, sceneData.Geometry.BaseParcelPosition, false)
                .EnsureNotNull();

            if (!World.Has<SDKTransform>(cameraEntity))
            {
                var newComponent = sdkTransformPool.Get();
                // Copy all fields
                newComponent.Position = sdkTransform.Position;
                newComponent.Rotation = sdkTransform.Rotation;
                newComponent.Scale = sdkTransform.Scale;

                World.Add(cameraEntity, newComponent);
            }
            
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
