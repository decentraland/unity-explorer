using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using Cinemachine;
using CRDT;
using CrdtEcsBridge.Components;
using DCL.CharacterCamera;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.CameraControl.MainCamera.Components;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
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
        private readonly IComponentPool<CinemachineFreeLook> poolRegistry;
        private readonly Dictionary<CRDTEntity,Entity> entitiesMap;
        private readonly Entity cameraEntity;
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly IExposedCameraData cameraData;

        public MainCameraSystem(
            World world,
            IComponentPool<CinemachineFreeLook> poolRegistry,
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
            SetupVirtualCameraQuery(World);
            SetupMainCameraQuery(World);

            HandleActiveVirtualCameraDirtyStateQuery(World);
            HandleVirtualCameraChangeQuery(World);
            DisableVirtualCameraOnSceneLeaveQuery(World);

            HandleVirtualCameraRemovalQuery(World);
            HandleMainCameraRemovalQuery(World);
            HandleVirtualCameraEntityDestructionQuery(World);
            HandleMainCameraEntityDestructionQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void HandleVirtualCameraChange(Entity entity, ref MainCameraComponent mainCameraComponent, in PBMainCamera pbMainCamera)
        {
            if (entity != cameraEntity || !sceneStateProvider.IsCurrent) return;

            // Cannot check by pbComponent.IsDirty since the VirtualCamera may not yet be on the target CRDTEntity
            // when the pbComponent is dirty and may have to be re-checked on subsequent updates...
            if (pbMainCamera.VirtualCameraEntity == mainCameraComponent.virtualCameraCRDTEntity &&
                 (mainCameraComponent.virtualCameraInstance == null || mainCameraComponent.virtualCameraInstance.enabled))
                return;

            int virtualCameraCRDTEntity = (int)pbMainCamera.VirtualCameraEntity;
            mainCameraComponent.virtualCameraCRDTEntity = virtualCameraCRDTEntity;

            CinemachineFreeLook? previousVirtualCamera = mainCameraComponent.virtualCameraInstance;
            bool hasPreviousVirtualCamera = previousVirtualCamera != null && previousVirtualCamera.enabled;
            if (virtualCameraCRDTEntity > 0)
            {
                Vector3 cinemachineCurrentActiveCamPos = cameraData.CinemachineBrain!.ActiveVirtualCamera.VirtualCameraGameObject.transform.position;
                ApplyVirtualCamera(
                    ref mainCameraComponent,
                    virtualCameraCRDTEntity,
                    hasPreviousVirtualCamera ? previousVirtualCamera!.transform.position : cinemachineCurrentActiveCamPos
                );
            }
            else
            {
                mainCameraComponent.virtualCameraInstance = null;
            }

            if (hasPreviousVirtualCamera)
                previousVirtualCamera!.enabled = false;
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void HandleActiveVirtualCameraDirtyState(CRDTEntity crdtEntity, in PBVirtualCamera pbVirtualCamera, ref VirtualCameraComponent virtualCameraComponent)
        {
            if (!pbVirtualCamera.IsDirty || cameraData.CinemachineBrain!.ActiveVirtualCamera.VirtualCameraGameObject != virtualCameraComponent.virtualCameraInstance.gameObject) return;

            virtualCameraComponent.lookAtCRDTEntity = GetPBVirtualCameraLookAtCRDTEntity(pbVirtualCamera, crdtEntity.Id);
            ConfigureCameraLookAt(virtualCameraComponent);
        }

        [Query]
        private void DisableVirtualCameraOnSceneLeave(in MainCameraComponent mainCameraComponent)
        {
            if (sceneStateProvider.IsCurrent) return;

            DisableActiveVirtualCamera(mainCameraComponent);
        }

        [Query]
        [All(typeof(PBMainCamera))]
        [None(typeof(MainCameraComponent), typeof(DeleteEntityIntention))]
        private void SetupMainCamera(Entity entity)
        {
            if (!sceneStateProvider.IsCurrent || entity != cameraEntity) return;

            World.Add(entity, new MainCameraComponent());
        }

        [Query]
        [None(typeof(VirtualCameraComponent), typeof(DeleteEntityIntention))]
        private void SetupVirtualCamera(Entity entity, CRDTEntity crdtEntity, in PBVirtualCamera pbVirtualCamera, in TransformComponent transform)
        {
            if (!sceneStateProvider.IsCurrent) return;

            var virtualCameraInstance = poolRegistry.Get();
            virtualCameraInstance.transform.SetParent(transform.Transform);
            virtualCameraInstance.transform.localPosition = Vector3.zero;
            virtualCameraInstance.transform.localRotation = Quaternion.identity;
            World.Add(entity, new VirtualCameraComponent(virtualCameraInstance, GetPBVirtualCameraLookAtCRDTEntity(pbVirtualCamera, crdtEntity.Id)));
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(PBVirtualCamera))]
        private void HandleVirtualCameraRemoval(Entity entity, in VirtualCameraComponent component)
        {
            component.virtualCameraInstance.enabled = false;
            poolRegistry.Release(component.virtualCameraInstance);
            World.Remove<VirtualCameraComponent>(entity);
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(PBMainCamera))]
        private void HandleMainCameraRemoval(Entity entity, in MainCameraComponent component)
        {
            DisableActiveVirtualCamera(component);
            World.Remove<MainCameraComponent>(entity);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleVirtualCameraEntityDestruction(in VirtualCameraComponent component)
        {
            component.virtualCameraInstance.enabled = false;
            poolRegistry.Release(component.virtualCameraInstance);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleMainCameraEntityDestruction(in MainCameraComponent component)
        {
            DisableActiveVirtualCamera(component);
        }

        [Query]
        private void FinalizeMainCameraComponent(in MainCameraComponent mainCameraComponent)
        {
            DisableActiveVirtualCamera(mainCameraComponent);
        }

        [Query]
        private void FinalizeVirtualCameraComponents(in VirtualCameraComponent virtualCameraComponent)
        {
            poolRegistry.Release(virtualCameraComponent.virtualCameraInstance);
        }

        public void FinalizeComponents(in Query query)
        {
            FinalizeMainCameraComponentQuery(World);
            FinalizeVirtualCameraComponentsQuery(World);
        }

        private int GetPBVirtualCameraLookAtCRDTEntity(in PBVirtualCamera pbVirtualCamera, int virtualCameraCRDTEntity)
        {
            if (pbVirtualCamera.HasLookAtEntity)
            {
                int targetEntity = (int)pbVirtualCamera.LookAtEntity;
                if (targetEntity != SpecialEntitiesID.CAMERA_ENTITY && targetEntity != virtualCameraCRDTEntity)
                    return targetEntity;
            }

            return -1;
        }

        private void ApplyVirtualCamera(ref MainCameraComponent mainCameraComponent, int virtualCamCRDTEntity, Vector3? previousCameraPosition)
        {
            if (!TryGetVirtualCameraComponent(virtualCamCRDTEntity, out var virtualCameraComponent)) return;

            var virtualCameraInstance = virtualCameraComponent!.Value.virtualCameraInstance;

            ConfigureVirtualCameraTransition(virtualCamCRDTEntity,
                previousCameraPosition.HasValue ? Vector3.Distance(virtualCameraInstance.transform.position, previousCameraPosition.Value) : 0f);

            ConfigureCameraLookAt(virtualCameraComponent.Value);

            mainCameraComponent.virtualCameraCRDTEntity = virtualCamCRDTEntity;
            mainCameraComponent.virtualCameraInstance = virtualCameraInstance;
            virtualCameraInstance.enabled = true;
        }

        private void ConfigureVirtualCameraTransition(int virtualCamCRDTEntity, float distanceBetweenCameras)
        {
            var pbVirtualCamera = World.Get<PBVirtualCamera>(entitiesMap[virtualCamCRDTEntity]);

            // Using custom blends array doesn't work because there's no direct way of getting the custom blend index,
            // and we would have to hardcode it...
            if (pbVirtualCamera.DefaultTransition.TransitionModeCase == CameraTransition.TransitionModeOneofCase.Time)
            {
                float timeValue = pbVirtualCamera.DefaultTransition.Time;
                cameraData.CinemachineBrain!.m_DefaultBlend.m_Time = timeValue;
                cameraData.CinemachineBrain!.m_DefaultBlend.m_Style = timeValue <= 0 ? CinemachineBlendDefinition.Style.Cut : CinemachineBlendDefinition.Style.EaseInOut;
            }
            else
            {
                float speedValue = pbVirtualCamera.DefaultTransition.Speed;
                cameraData.CinemachineBrain!.m_DefaultBlend.m_Style = speedValue <= 0 ? CinemachineBlendDefinition.Style.Cut : CinemachineBlendDefinition.Style.EaseInOut;

                // SPEED = 1 -> 1 meter per second
                float blendTime = distanceBetweenCameras / speedValue;
                if (blendTime == 0)
                    cameraData.CinemachineBrain!.m_DefaultBlend.m_Style = CinemachineBlendDefinition.Style.Cut;
                else
                    cameraData.CinemachineBrain!.m_DefaultBlend.m_Time = blendTime;
            }
        }

        private void ConfigureCameraLookAt(in VirtualCameraComponent virtualCameraComponent)
        {
            var rig = virtualCameraComponent.virtualCameraInstance.GetRig(1); // Middle (Aiming) Rig
            if (entitiesMap.TryGetValue(virtualCameraComponent.lookAtCRDTEntity, out Entity lookAtEntity)
                && World.TryGet(lookAtEntity, out TransformComponent transformComponent))
            {
                virtualCameraComponent.virtualCameraInstance.m_LookAt = transformComponent.Transform;
                rig.AddCinemachineComponent<CinemachineHardLookAt>();
            }
            else
            {
                rig.AddCinemachineComponent<CinemachinePOV>();
                virtualCameraComponent.virtualCameraInstance.m_LookAt = null;
            }
        }

        private bool TryGetVirtualCameraComponent(int targetCRDTEntity, out VirtualCameraComponent? returnComponent)
        {
            returnComponent = null;
            if (!entitiesMap.TryGetValue(targetCRDTEntity, out Entity virtualCameraEntity)
                || !World.TryGet(virtualCameraEntity, out VirtualCameraComponent virtualCameraComponent))
                return false;

            returnComponent = virtualCameraComponent;

            return true;
        }

        private void DisableActiveVirtualCamera(in MainCameraComponent mainCameraComponent)
        {
            if (mainCameraComponent.virtualCameraInstance != null && mainCameraComponent.virtualCameraInstance.enabled)
                mainCameraComponent.virtualCameraInstance.enabled = false;
        }
    }
}
