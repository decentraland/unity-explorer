using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using Cinemachine;
using CRDT;
using DCL.CharacterCamera;
using DCL.ECSComponents;
using DCL.SceneRestrictionBusController.SceneRestriction;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
using DCL.SDKComponents.CameraControl.MainCamera.Components;
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
    [LogCategory(ReportCategory.SDK_CAMERA)]
    public partial class MainCameraSystem : BaseUnityLoopSystem, IFinalizeWorldSystem, ISceneIsCurrentListener
    {
        private readonly IReadOnlyDictionary<CRDTEntity,Entity> entitiesMap;
        private readonly Entity cameraEntity;
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly IExposedCameraData cameraData;
        private readonly ISceneRestrictionBusController sceneRestrictionBusController;
        private readonly World globalWorld;
        private CameraMode lastNonSDKCameraMode;

        public MainCameraSystem(
            World world,
            Entity cameraEntity,
            Dictionary<CRDTEntity,Entity> entitiesMap,
            ISceneStateProvider sceneStateProvider,
            IExposedCameraData cameraData,
            ISceneRestrictionBusController sceneRestrictionBusController,
            World globalWorld) : base(world)
        {
            this.cameraEntity = cameraEntity;
            this.entitiesMap = entitiesMap;
            this.sceneStateProvider = sceneStateProvider;
            this.cameraData = cameraData;
            this.globalWorld = globalWorld;
            this.sceneRestrictionBusController = sceneRestrictionBusController;
        }

        protected override void Update(float t)
        {
            SetupMainCameraQuery(World);

            HandleActiveVirtualCameraLookAtChangeQuery(World);
            HandleVirtualCameraChangeQuery(World);

            HandleMainCameraRemovalQuery(World);
            HandleMainCameraEntityDestructionQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void HandleVirtualCameraChange(Entity entity, ref MainCameraComponent mainCameraComponent, in PBMainCamera pbMainCamera)
        {
            if (entity != cameraEntity || !sceneStateProvider.IsCurrent) return;

            CRDTEntity? virtualCameraCRDTEntity = pbMainCamera.HasVirtualCameraEntity ? new CRDTEntity((int)pbMainCamera.VirtualCameraEntity) : null;

            // Cannot rely on pbComponent.IsDirty since the VirtualCamera may not yet be on the target CRDTEntity
            // when the pbComponent is dirty and may have to be re-checked on subsequent updates. This can happen
            // if the target entity/component hasn't been loaded/detected from CRDT yet.
            if (mainCameraComponent.virtualCameraCRDTEntity.HasValue && virtualCameraCRDTEntity.HasValue &&
                mainCameraComponent.virtualCameraCRDTEntity.Value.Id == virtualCameraCRDTEntity.Value.Id &&
                (mainCameraComponent.virtualCameraInstance == null || mainCameraComponent.virtualCameraInstance.enabled))
                return;

            CinemachineFreeLook? previousVirtualCamera = mainCameraComponent.virtualCameraInstance;
            bool hasPreviousVirtualCamera = previousVirtualCamera != null && previousVirtualCamera.enabled;
            if (virtualCameraCRDTEntity.HasValue)
            {
                Vector3 cinemachineCurrentActiveCamPos = cameraData.CinemachineBrain!.ActiveVirtualCamera.VirtualCameraGameObject.transform.position;

                // It may take more than 1 run to detect the VirtualCamera component on the crdt entity
                if (!TryApplyVirtualCamera(
                        ref mainCameraComponent,
                        virtualCameraCRDTEntity.Value,
                        hasPreviousVirtualCamera ? previousVirtualCamera!.transform.position : cinemachineCurrentActiveCamPos))
                    return;

                // virtualCameraCRDTEntity assigned only after successfully applying it, so that
                // the system keeps trying otherwise
                mainCameraComponent.virtualCameraCRDTEntity = virtualCameraCRDTEntity;
            }
            else
            {
                mainCameraComponent.virtualCameraCRDTEntity = null;
                mainCameraComponent.virtualCameraInstance = null;
            }

            if (hasPreviousVirtualCamera)
            {
                previousVirtualCamera!.enabled = false;
                sceneRestrictionBusController.PushSceneRestriction(SceneRestriction.CreateCameraLocked(SceneRestrictionsAction.REMOVED));
            }

            UpdateGlobalWorldCameraMode(mainCameraComponent.virtualCameraInstance != null);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void HandleActiveVirtualCameraLookAtChange(CRDTEntity crdtEntity, in PBVirtualCamera pbVirtualCamera, ref VirtualCameraComponent virtualCameraComponent)
        {
            if (!sceneStateProvider.IsCurrent || cameraData.CinemachineBrain!.ActiveVirtualCamera.VirtualCameraGameObject != virtualCameraComponent.virtualCameraInstance.gameObject) return;

            CRDTEntity? pbVirtualCameraLookAtEntity = VirtualCameraUtils.GetPBVirtualCameraLookAtCRDTEntity(pbVirtualCamera, crdtEntity);

            if (pbVirtualCameraLookAtEntity.Equals(virtualCameraComponent.lookAtCRDTEntity)) return;

            virtualCameraComponent.lookAtCRDTEntity = pbVirtualCameraLookAtEntity;
            VirtualCameraUtils.ConfigureCameraLookAt(World, entitiesMap, virtualCameraComponent);
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

        private bool TryApplyVirtualCamera(ref MainCameraComponent mainCameraComponent, CRDTEntity virtualCamCRDTEntity, Vector3? previousCameraPosition)
        {
            if (!VirtualCameraUtils.TryGetVirtualCameraComponents(
                    World,
                    entitiesMap,
                    virtualCamCRDTEntity,
                    out var virtualCameraComponent,
                    out var pbVirtualCameraComponent))
                return false;

            var virtualCameraInstance = virtualCameraComponent!.Value.virtualCameraInstance;

            VirtualCameraUtils.ConfigureVirtualCameraTransition(cameraData, pbVirtualCameraComponent!,
                previousCameraPosition.HasValue ? Vector3.Distance(virtualCameraInstance.transform.position, previousCameraPosition.Value) : 0f);

            VirtualCameraUtils.ConfigureCameraLookAt(World, entitiesMap, virtualCameraComponent.Value);

            VirtualCameraUtils.ConfigureVirtualCameraFOV(pbVirtualCameraComponent!, virtualCameraComponent.Value);

            mainCameraComponent.virtualCameraCRDTEntity = virtualCamCRDTEntity;
            mainCameraComponent.virtualCameraInstance = virtualCameraInstance;
            virtualCameraInstance.enabled = true;

            sceneRestrictionBusController.PushSceneRestriction(SceneRestriction.CreateCameraLocked(SceneRestrictionsAction.APPLIED));

            return true;
        }

        private void UpdateGlobalWorldCameraMode(bool isAnyVirtualCameraActive)
        {
            ref CameraComponent cameraComponent = ref globalWorld.Get<CameraComponent>(cameraData.CameraEntityProxy.Object);
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
        }

        private void DisableActiveVirtualCamera(in MainCameraComponent mainCameraComponent)
        {
            if (mainCameraComponent.virtualCameraInstance == null
                || !mainCameraComponent.virtualCameraInstance.enabled
                || !VirtualCameraUtils.VirtualCameraExistsInEntitiesMap(entitiesMap, mainCameraComponent.virtualCameraCRDTEntity))
                return;

            mainCameraComponent.virtualCameraInstance.enabled = false;

            UpdateGlobalWorldCameraMode(false);
        }

        // Scene is unloaded
        public void FinalizeComponents(in Query query)
        {
            FinalizeMainCameraComponentQuery(World);
        }

        [Query]
        private void FinalizeMainCameraComponent(in MainCameraComponent mainCameraComponent)
        {
            DisableActiveVirtualCamera(mainCameraComponent);
            sceneRestrictionBusController.PushSceneRestriction(SceneRestriction.CreateCameraLocked(SceneRestrictionsAction.REMOVED));
        }

        // User leaves the scene
        public void OnSceneIsCurrentChanged(bool value)
        {
            if (value) return;

            DisableVirtualCameraOnSceneLeaveQuery(World);
        }

        [Query]
        private void DisableVirtualCameraOnSceneLeave(in MainCameraComponent mainCameraComponent)
        {
            DisableActiveVirtualCamera(mainCameraComponent);
        }
    }
}
