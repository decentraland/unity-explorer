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
        private static readonly QueryDescription GLOBAL_WORLD_CAMERA_QUERY = new QueryDescription().WithAll<CameraComponent>();

        private readonly Dictionary<CRDTEntity,Entity> entitiesMap;
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

            int virtualCameraCRDTEntity = (int)pbMainCamera.VirtualCameraEntity;

            // Cannot check by pbComponent.IsDirty since the VirtualCamera may not yet be on the target CRDTEntity
            // when the pbComponent is dirty and may have to be re-checked on subsequent updates...
            if (mainCameraComponent.virtualCameraCRDTEntity == virtualCameraCRDTEntity &&
                (mainCameraComponent.virtualCameraInstance == null || mainCameraComponent.virtualCameraInstance.enabled))
                return;

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

            UpdateGlobalWorldCameraMode(mainCameraComponent.virtualCameraInstance != null);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void HandleActiveVirtualCameraDirtyState(CRDTEntity crdtEntity, in PBVirtualCamera pbVirtualCamera, ref VirtualCameraComponent virtualCameraComponent)
        {
            if (!pbVirtualCamera.IsDirty || cameraData.CinemachineBrain!.ActiveVirtualCamera.VirtualCameraGameObject != virtualCameraComponent.virtualCameraInstance.gameObject) return;

            virtualCameraComponent.lookAtCRDTEntity = VirtualCameraUtils.GetPBVirtualCameraLookAtCRDTEntity(pbVirtualCamera, crdtEntity.Id);
            VirtualCameraUtils.ConfigureCameraLookAt(World, in entitiesMap, virtualCameraComponent);
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

        private void ApplyVirtualCamera(ref MainCameraComponent mainCameraComponent, int virtualCamCRDTEntity, Vector3? previousCameraPosition)
        {
            if (!VirtualCameraUtils.TryGetVirtualCameraComponent(World, in entitiesMap, virtualCamCRDTEntity, out var virtualCameraComponent)) return;

            var virtualCameraInstance = virtualCameraComponent!.Value.virtualCameraInstance;

            VirtualCameraUtils.ConfigureVirtualCameraTransition(World, in entitiesMap, cameraData, virtualCamCRDTEntity,
                previousCameraPosition.HasValue ? Vector3.Distance(virtualCameraInstance.transform.position, previousCameraPosition.Value) : 0f);

            VirtualCameraUtils.ConfigureCameraLookAt(World, in entitiesMap, virtualCameraComponent.Value);

            mainCameraComponent.virtualCameraCRDTEntity = virtualCamCRDTEntity;
            mainCameraComponent.virtualCameraInstance = virtualCameraInstance;
            virtualCameraInstance.enabled = true;
        }

        private void UpdateGlobalWorldCameraMode(bool isAnyVirtualCameraActive)
        {
            globalWorld.Query(in GLOBAL_WORLD_CAMERA_QUERY,
                (ref CameraComponent cameraComponent) =>
                {
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
                });
        }

        private void DisableActiveVirtualCamera(in MainCameraComponent mainCameraComponent)
        {
            if (mainCameraComponent.virtualCameraInstance != null && mainCameraComponent.virtualCameraInstance.enabled)
                mainCameraComponent.virtualCameraInstance.enabled = false;

            UpdateGlobalWorldCameraMode(false);
        }
    }
}
