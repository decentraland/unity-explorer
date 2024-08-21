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
                ApplyVirtualCamera(ref mainCameraComponent, (int)pbMainCamera.VirtualCameraEntity);

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

        private void ApplyVirtualCamera(ref MainCameraComponent mainCameraComponent, int virtualCamCRDTEntity)
        {
            if (!TryGetCinemachineVirtualCamera(virtualCamCRDTEntity, out var virtualCameraInstance)) return;

            ConfigureVirtualCameraTransition(virtualCamCRDTEntity);

            mainCameraComponent.virtualCameraCRDTEntity = virtualCamCRDTEntity;
            mainCameraComponent.virtualCameraInstance = virtualCameraInstance;
            virtualCameraInstance!.enabled = true;
        }

        private void ConfigureVirtualCameraTransition(int virtualCamCRDTEntity)
        {
            var pbVirtualCamera = World.Get<PBVirtualCamera>(entitiesMap[virtualCamCRDTEntity]);

            // Using custom blends array doesn't work because there's no direct way of getting the custom blend index,
            // and we would have to hardcode it...
            var brain = UnityEngine.GameObject.FindObjectOfType<CinemachineBrain>(); // TODO: Inject from somewhere...

            if (pbVirtualCamera.DefaultTransition.TransitionCase == CameraTransition.TransitionOneofCase.Time)
            {
                brain.m_DefaultBlend.m_Style = pbVirtualCamera.DefaultTransition.Time.Value <= 0 ? CinemachineBlendDefinition.Style.Cut : CinemachineBlendDefinition.Style.EaseInOut;
                brain.m_DefaultBlend.m_Time = pbVirtualCamera.DefaultTransition.Time.Value;
            }
            // else
            // {
            //     // calculate time
            //     sdkVirtualCameraBlend.
            // }
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
