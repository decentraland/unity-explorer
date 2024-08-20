using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using Cinemachine;
using CRDT;
using CrdtEcsBridge.Components.Transform;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.CameraControl.MainCamera.Components;
using DCL.Utilities;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.Unity.Transforms.Components;
using System.Collections.Generic;

namespace DCL.SDKComponents.CameraControl.MainCamera.Systems
{
    [UpdateInGroup(typeof(SyncedInitializationFixedUpdateThrottledGroup))]
    [LogCategory(ReportCategory.SDK_MAIN_CAMERA)]
    public partial class MainCameraSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly IComponentPool<CinemachineVirtualCamera> poolRegistry;
        private readonly Dictionary<CRDTEntity,Entity> entitiesMap;
        private readonly Entity cameraEntity;

        public MainCameraSystem(
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
            SetupMainCameraQuery(World);
            UpdateMainCameraQuery(World);

            SetupVirtualCameraQuery(World);

            // HandleEntityDestructionQuery(World);
            // HandleComponentRemovalQuery(World);
        }

        [Query]
        private void UpdateMainCamera(in Entity entity, ref MainCameraComponent mainCameraComponent, PBMainCamera pbMainCamera)
        {
            if (entity != cameraEntity) return;

            // Cannot check by pbComponent.IsDirty since the VirtualCamera may not yet be on the target CRDTEntity
            // when the pbComponent is dirty...
            if (pbMainCamera.VirtualCameraEntity == mainCameraComponent.virtualCameraCRDTEntity) return;

            CinemachineVirtualCamera? oldVirtualCamera = mainCameraComponent.virtualCameraInstance;
            mainCameraComponent.virtualCameraInstance = null;

            if (pbMainCamera.VirtualCameraEntity > 0)
            {
                int virtualCamCRDTEntity = (int)pbMainCamera.VirtualCameraEntity;
                if (TryGetCinemachineVirtualCamera(virtualCamCRDTEntity, out var virtualCameraInstance))
                {
                    mainCameraComponent.virtualCameraCRDTEntity = virtualCamCRDTEntity;
                    mainCameraComponent.virtualCameraInstance = virtualCameraInstance;
                    virtualCameraInstance!.enabled = true;
                }
            }

            if (oldVirtualCamera != null)
                oldVirtualCamera.enabled = false;
        }

        [Query]
        [All(typeof(PBMainCamera))]
        [None(typeof(MainCameraComponent))]
        private void SetupMainCamera(in Entity entity)
        {
            if (entity != cameraEntity) return;

            World.Add(entity, new MainCameraComponent());
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
