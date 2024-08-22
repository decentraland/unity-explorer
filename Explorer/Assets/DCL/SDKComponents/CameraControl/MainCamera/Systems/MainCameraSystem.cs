using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using Cinemachine;
using CRDT;
using DCL.CharacterCamera;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.CameraControl.MainCamera.Components;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.Unity.Transforms.Components;
using SceneRunner.Scene;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.SDKComponents.CameraControl.MainCamera.Systems
{
    [UpdateInGroup(typeof(SyncedInitializationFixedUpdateThrottledGroup))]
    [LogCategory(ReportCategory.SDK_MAIN_CAMERA)]
    public partial class MainCameraSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly IComponentPool<CinemachineVirtualCamera> poolRegistry;
        private readonly Dictionary<CRDTEntity,Entity> entitiesMap;
        private readonly Entity cameraEntity;
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly IExposedCameraData cameraData;

        public MainCameraSystem(
            World world,
            IComponentPool<CinemachineVirtualCamera> poolRegistry,
            Entity cameraEntity,
            Dictionary<CRDTEntity,Entity> entitiesMap,
            ISceneStateProvider sceneStateProvider,
            IExposedCameraData cameraData) : base(world)
        {
            this.poolRegistry = poolRegistry;
            this.cameraEntity = cameraEntity;
            this.entitiesMap = entitiesMap;
            this.sceneStateProvider = sceneStateProvider;
            this.cameraData = cameraData;
        }

        protected override void Update(float t)
        {
            if (cameraData.CinemachineBrain == null) return;

            SetupVirtualCameraQuery(World);
            SetupMainCameraQuery(World);
            UpdateMainCameraQuery(World);

            // HandleEntityDestructionQuery(World);
            // HandleComponentRemovalQuery(World);
        }

        [Query]
        private void UpdateMainCamera(in Entity entity, ref MainCameraComponent mainCameraComponent, PBMainCamera pbMainCamera)
        {
            if (entity != cameraEntity) return;

            // Cannot check by pbComponent.IsDirty since the VirtualCamera may not yet be on the target CRDTEntity
            // when the pbComponent is dirty...
            if (sceneStateProvider.IsCurrent && pbMainCamera.VirtualCameraEntity == mainCameraComponent.virtualCameraCRDTEntity) return;

            CinemachineVirtualCamera? oldVirtualCamera = mainCameraComponent.virtualCameraInstance;
            mainCameraComponent.virtualCameraInstance = null;

            if (sceneStateProvider.IsCurrent && pbMainCamera.VirtualCameraEntity > 0)
                ApplyVirtualCamera(
                        ref mainCameraComponent,
                        (int)pbMainCamera.VirtualCameraEntity,
                        oldVirtualCamera != null ? oldVirtualCamera.transform.position : cameraData.CinemachineBrain.ActiveVirtualCamera.VirtualCameraGameObject.transform.position
                    );

            if (oldVirtualCamera != null)
                oldVirtualCamera.enabled = false;
        }

        [Query]
        [All(typeof(PBMainCamera))]
        [None(typeof(MainCameraComponent))]
        private void SetupMainCamera(in Entity entity)
        {
            if (!sceneStateProvider.IsCurrent || entity != cameraEntity) return;

            World.Add(entity, new MainCameraComponent());
        }

        [Query]
        [All(typeof(PBVirtualCamera))]
        [None(typeof(VirtualCameraComponent))]
        private void SetupVirtualCamera(in Entity entity, TransformComponent transform)
        {
            if (!sceneStateProvider.IsCurrent) return;

            var virtualCameraInstance = poolRegistry.Get();
            virtualCameraInstance.transform.SetParent(transform.Transform);
            virtualCameraInstance.transform.localPosition = Vector3.zero;
            virtualCameraInstance.transform.localRotation = Quaternion.identity;
            World.Add(entity, new VirtualCameraComponent(virtualCameraInstance));
        }

        public void FinalizeComponents(in Query query)
        {
            // throw new NotImplementedException();
        }

        private void ApplyVirtualCamera(ref MainCameraComponent mainCameraComponent, int virtualCamCRDTEntity, Vector3? previousCameraPosition)
        {
            if (!TryGetCinemachineVirtualCamera(virtualCamCRDTEntity, out var virtualCameraInstance)) return;

            ConfigureVirtualCameraTransition(virtualCamCRDTEntity,
                previousCameraPosition.HasValue ? Vector3.Distance(virtualCameraInstance!.transform.position, previousCameraPosition.Value) : 0f);

            mainCameraComponent.virtualCameraCRDTEntity = virtualCamCRDTEntity;
            mainCameraComponent.virtualCameraInstance = virtualCameraInstance;
            virtualCameraInstance!.enabled = true;
        }

        private void ConfigureVirtualCameraTransition(int virtualCamCRDTEntity, float distanceBetweenCameras)
        {
            var pbVirtualCamera = World.Get<PBVirtualCamera>(entitiesMap[virtualCamCRDTEntity]);

            // Using custom blends array doesn't work because there's no direct way of getting the custom blend index,
            // and we would have to hardcode it...
            if (pbVirtualCamera.DefaultTransition.TransitionCase == CameraTransition.TransitionOneofCase.Time)
            {
                float timeValue = pbVirtualCamera.DefaultTransition.Time.Value;
                cameraData.CinemachineBrain!.m_DefaultBlend.m_Time = timeValue;
                cameraData.CinemachineBrain!.m_DefaultBlend.m_Style = timeValue <= 0 ? CinemachineBlendDefinition.Style.Cut : CinemachineBlendDefinition.Style.EaseInOut;
            }
            else
            {
                float speedValue = pbVirtualCamera.DefaultTransition.Speed.Value;
                cameraData.CinemachineBrain!.m_DefaultBlend.m_Style = speedValue <= 0 ? CinemachineBlendDefinition.Style.Cut : CinemachineBlendDefinition.Style.EaseInOut;

                // SPEED = 1 -> 1 Meter per second
                float blendTime = distanceBetweenCameras / speedValue;
                if (blendTime == 0)
                    cameraData.CinemachineBrain!.m_DefaultBlend.m_Style = CinemachineBlendDefinition.Style.Cut;
                else
                    cameraData.CinemachineBrain!.m_DefaultBlend.m_Time = blendTime;
            }
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
