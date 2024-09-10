using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using Cinemachine;
using CRDT;
using DCL.CharacterCamera;
using DCL.ECSComponents;
using DCL.SDKComponents.CameraControl.MainCamera.Components;
using DCL.SDKComponents.CameraModeArea.Systems;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using SceneRunner.Scene;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.SDKComponents.CameraControl.MainCamera.Systems
{
    [UpdateInGroup(typeof(SyncedInitializationFixedUpdateThrottledGroup))]
    [UpdateBefore(typeof(CameraModeAreaHandlerSystem))]
    [LogCategory(ReportCategory.SDK_CAMERA)]
    public partial class MainCameraSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly IReadOnlyDictionary<CRDTEntity,Entity> entitiesMap;
        private readonly Entity cameraEntity;
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly IExposedCameraData cameraData;
        private readonly World globalWorld;
        private CameraMode lastNonSDKCameraMode;

        public MainCameraSystem(
            World world,
            Entity cameraEntity,
            Dictionary<CRDTEntity,Entity> entitiesMap,
            ISceneStateProvider sceneStateProvider,
            IExposedCameraData cameraData,
            World globalWorld) : base(world)
        {
            this.cameraEntity = cameraEntity;
            this.entitiesMap = entitiesMap;
            this.sceneStateProvider = sceneStateProvider;
            this.cameraData = cameraData;
            this.globalWorld = globalWorld;
        }

        protected override void Update(float t)
        {
            SetupMainCameraQuery(World);

            HandleVirtualCameraChangeQuery(World);
            DisableVirtualCameraOnSceneLeaveQuery(World);

            HandleMainCameraRemovalQuery(World);
            HandleMainCameraEntityDestructionQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void HandleVirtualCameraChange(Entity entity, ref MainCameraComponent mainCameraComponent, in PBMainCamera pbMainCamera)
        {
            if (entity != cameraEntity || !sceneStateProvider.IsCurrent) return;

            CRDTEntity virtualCameraCRDTEntity = new CRDTEntity((int)pbMainCamera.VirtualCameraEntity);

            // Cannot rely on pbComponent.IsDirty since the VirtualCamera may not yet be on the target CRDTEntity
            // when the pbComponent is dirty and may have to be re-checked on subsequent updates. This can happen
            // if the target entity/component hasn't been loaded/detected from CRDT yet.
            if (mainCameraComponent.virtualCameraCRDTEntity.Id == virtualCameraCRDTEntity.Id &&
                (mainCameraComponent.virtualCameraInstance == null || mainCameraComponent.virtualCameraInstance.enabled))
                return;

            mainCameraComponent.virtualCameraCRDTEntity = virtualCameraCRDTEntity;

            CinemachineFreeLook? previousVirtualCamera = mainCameraComponent.virtualCameraInstance;
            bool hasPreviousVirtualCamera = previousVirtualCamera != null && previousVirtualCamera.enabled;
            if (virtualCameraCRDTEntity.Id > 0)
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

            UpdateGlobalWorldCameraMode(mainCameraComponent.virtualCameraInstance != null);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void HandleActiveVirtualCameraDirtyState(CRDTEntity crdtEntity, in PBVirtualCamera pbVirtualCamera, ref VirtualCameraComponent virtualCameraComponent)
        {
            if (!pbVirtualCamera.IsDirty || cameraData.CinemachineBrain!.ActiveVirtualCamera.VirtualCameraGameObject != virtualCameraComponent.virtualCameraInstance.gameObject) return;

            virtualCameraComponent.lookAtCRDTEntity = VirtualCameraUtils.GetPBVirtualCameraLookAtCRDTEntity(pbVirtualCamera, crdtEntity);
            VirtualCameraUtils.ConfigureCameraLookAt(World, entitiesMap, virtualCameraComponent);
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

            World.Add(entity, new MainCameraComponent(new CRDTEntity(0)));
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
        private void HandleMainCameraEntityDestruction(in MainCameraComponent component)
        {
            DisableActiveVirtualCamera(component);
        }

        [Query]
        private void FinalizeMainCameraComponent(in MainCameraComponent mainCameraComponent)
        {
            DisableActiveVirtualCamera(mainCameraComponent);
        }

        public void FinalizeComponents(in Query query)
        {
            FinalizeMainCameraComponentQuery(World);
        }

        private void ApplyVirtualCamera(ref MainCameraComponent mainCameraComponent, CRDTEntity virtualCamCRDTEntity, Vector3? previousCameraPosition)
        {
            if (!VirtualCameraUtils.TryGetVirtualCameraComponent(World, entitiesMap, virtualCamCRDTEntity, out var virtualCameraComponent)) return;

            var virtualCameraInstance = virtualCameraComponent!.Value.virtualCameraInstance;

            VirtualCameraUtils.ConfigureVirtualCameraTransition(World, entitiesMap, cameraData, virtualCamCRDTEntity,
                previousCameraPosition.HasValue ? Vector3.Distance(virtualCameraInstance.transform.position, previousCameraPosition.Value) : 0f);

            VirtualCameraUtils.ConfigureCameraLookAt(World, entitiesMap, virtualCameraComponent.Value);

            mainCameraComponent.virtualCameraCRDTEntity = virtualCamCRDTEntity;
            mainCameraComponent.virtualCameraInstance = virtualCameraInstance;
            virtualCameraInstance.enabled = true;
        }

        private void UpdateGlobalWorldCameraMode(bool isAnyVirtualCameraActive)
        {
            var cameraComponent = globalWorld.Get<CameraComponent>(cameraData.CameraEntityProxy.Object);
            if (isAnyVirtualCameraActive)
            {
                if (cameraComponent.Mode != CameraMode.SDKCamera)
                {
                    lastNonSDKCameraMode = cameraComponent.Mode;
                    cameraComponent.Mode = CameraMode.SDKCamera;
                }
            }
            else if (cameraComponent.Mode == CameraMode.SDKCamera)
            {
                cameraComponent.Mode = lastNonSDKCameraMode;
            }
            globalWorld.Set(cameraData.CameraEntityProxy.Object, cameraComponent);
        }

        private void DisableActiveVirtualCamera(in MainCameraComponent mainCameraComponent)
        {
            if (mainCameraComponent.virtualCameraInstance != null && mainCameraComponent.virtualCameraInstance.enabled)
                mainCameraComponent.virtualCameraInstance.enabled = false;

            UpdateGlobalWorldCameraMode(false);
        }
    }
}
