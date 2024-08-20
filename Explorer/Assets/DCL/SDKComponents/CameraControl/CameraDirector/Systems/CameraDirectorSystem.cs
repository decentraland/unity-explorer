using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using Cinemachine;
using CRDT;
using CrdtEcsBridge.Components.Transform;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.CameraControl.CameraDirector.Components;
using DCL.Utilities;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.Unity.Transforms.Components;
using System.Collections.Generic;

namespace DCL.SDKComponents.CameraControl.CameraDirector.Systems
{
    [UpdateInGroup(typeof(SyncedInitializationFixedUpdateThrottledGroup))]
    [LogCategory(ReportCategory.CAMERA_DIRECTOR)]
    public partial class CameraDirectorSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly IComponentPool<CinemachineVirtualCamera> poolRegistry;
        private readonly Dictionary<CRDTEntity,Entity> entitiesMap;
        private readonly Entity cameraEntity;

        public CameraDirectorSystem(
            World world,
            IComponentPool<CinemachineVirtualCamera> poolRegistry,
            Entity cameraEntity,
            Dictionary<CRDTEntity,Entity> entitiesMap) : base(world)
        {
            this.poolRegistry = poolRegistry;
            this.cameraEntity = cameraEntity;
            this.entitiesMap = entitiesMap;
        }

        protected override void Update(float t)
        {
            SetupCameraDirectorQuery(World);
            UpdateCameraDirectorQuery(World);

            SetupVirtualCameraQuery(World);

            // HandleEntityDestructionQuery(World);
            // HandleComponentRemovalQuery(World);
        }

        [Query]
        private void UpdateCameraDirector(in Entity entity, ref CameraDirectorComponent cameraDirectorComponent, PBCameraDirector pbCameraDirector)
        {
            if (entity != cameraEntity) return;

            // Cannot check by pbComponent.IsDirty since the VirtualCamera may not yet be on the target CRDTEntity
            // when the pbComponent is dirty...
            if (pbCameraDirector.VirtualCameraEntity == cameraDirectorComponent.virtualCameraCRDTEntity) return;

            CinemachineVirtualCamera? oldVirtualCamera = cameraDirectorComponent.virtualCameraInstance;
            cameraDirectorComponent.virtualCameraInstance = null;

            if (pbCameraDirector.VirtualCameraEntity > 0)
            {
                int virtualCamCRDTEntity = (int)pbCameraDirector.VirtualCameraEntity;
                if (TryGetCinemachineVirtualCamera(virtualCamCRDTEntity, out var virtualCameraInstance))
                {
                    cameraDirectorComponent.virtualCameraCRDTEntity = virtualCamCRDTEntity;
                    cameraDirectorComponent.virtualCameraInstance = virtualCameraInstance;
                    virtualCameraInstance!.enabled = true;
                }
            }

            if (oldVirtualCamera != null)
                oldVirtualCamera.enabled = false;
        }

        [Query]
        [All(typeof(PBCameraDirector))]
        [None(typeof(CameraDirectorComponent))]
        private void SetupCameraDirector(in Entity entity)
        {
            if (entity != cameraEntity) return;

            World.Add(entity, new CameraDirectorComponent());
        }

        [Query]
        [None(typeof(VirtualCameraComponent))]
        private void SetupVirtualCamera(in Entity entity, TransformComponent transform, PBVirtualCamera pbVirtualCamera)
        {
            var virtualCameraInstance = poolRegistry.Get();
            virtualCameraInstance.transform.SetParent(transform.Transform);
            virtualCameraInstance.transform.localPosition = UnityEngine.Vector3.zero;
            virtualCameraInstance.transform.localRotation = UnityEngine.Quaternion.identity;

            // TODO: Use pbVirtualCamera values for transition speed/time...

            World.Add(entity, new VirtualCameraComponent(virtualCameraInstance));
        }

        public void FinalizeComponents(in Query query)
        {
            // throw new NotImplementedException();
        }

        private bool TryGetCinemachineVirtualCamera(CRDTEntity targetEntity, out CinemachineVirtualCamera? virtualCameraInstance)
        {
            virtualCameraInstance = null;
            if (!entitiesMap.TryGetValue(targetEntity, out Entity virtualCameraEntity)
                || !World.TryGet(virtualCameraEntity, out VirtualCameraComponent virtualCameraComponent))
                return false;

            virtualCameraInstance = virtualCameraComponent.virtualCameraInstance;

            return true;
        }
    }
}
