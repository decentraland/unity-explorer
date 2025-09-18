using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.CharacterCamera;
using DCL.SDKEntityTriggerArea.Components;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.InWorldCamera;
using DCL.SceneRestrictionBusController.SceneRestriction;
using DCL.SDKComponents.CameraModeArea.Components;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
using DCL.Utilities;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.Unity.Transforms.Components;
using System.Collections.Generic;

namespace DCL.SDKComponents.CameraModeArea.Systems
{
    [UpdateInGroup(typeof(SyncedInitializationSystemGroup))]
    [LogCategory(ReportCategory.CHARACTER_TRIGGER_AREA)]
    public partial class CameraModeAreaHandlerSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        // There's only 1 camera at a time, only 1 camera mode area effect at a time
        // and that area is only activated by the main player ('targetOnlyMainPlayer' property)
        // that's why we have these fields as static.
        private static CameraMode cameraModeBeforeLastAreaEnter;

        // Main player can enter an area while being already inside another one, but the last one
        // entered is the one in effect.
        private static readonly HashSet<Entity> activeAreas = new HashSet<Entity>();

        private readonly World globalWorld;
        private readonly ObjectProxy<Entity> cameraEntityProxy;
        private readonly IExposedCameraData cameraData;
        private readonly ISceneRestrictionBusController sceneRestrictionBusController;

        public CameraModeAreaHandlerSystem(World world, World globalWorld, ObjectProxy<Entity> cameraEntityProxy, IExposedCameraData cameraData, ISceneRestrictionBusController sceneRestrictionBusController) : base(world)
        {
            this.globalWorld = globalWorld;
            this.cameraEntityProxy = cameraEntityProxy;
            this.cameraData = cameraData;
            this.sceneRestrictionBusController = sceneRestrictionBusController;
        }

        protected override void Update(float t)
        {
            if (!cameraEntityProxy.Configured) return;

            UpdateCameraModeAreaQuery(World);
            SetupCameraModeAreaQuery(World);

            HandleComponentRemovalQuery(World);
            HandleEntityDestructionQuery(World);
        }

        [Query]
        [None(typeof(SDKEntityTriggerAreaComponent), typeof(CameraModeAreaComponent))]
        [All(typeof(TransformComponent))]
        private void SetupCameraModeArea(in Entity entity, ref PBCameraModeArea pbCameraModeArea)
        {
            World.Add(entity, new CameraModeAreaComponent(), new SDKEntityTriggerAreaComponent(areaSize: pbCameraModeArea.Area, targetOnlyMainPlayer: true));
        }

        [Query]
        [All(typeof(TransformComponent))]
        private void UpdateCameraModeArea(Entity entity, ref PBCameraModeArea pbCameraModeArea, ref SDKEntityTriggerAreaComponent sdkEntityTriggerAreaComponent)
        {
            if (pbCameraModeArea.IsDirty)
                sdkEntityTriggerAreaComponent.UpdateAreaSize(pbCameraModeArea.Area);

            if (cameraData.CameraMode == CameraMode.SDKCamera) return;

            if (sdkEntityTriggerAreaComponent.EnteredEntitiesToBeProcessed.Count > 0)
            {
                if (!activeAreas.Contains(entity))
                {
                    OnEnteredCameraModeArea((CameraMode)pbCameraModeArea.Mode);
                    activeAreas.Add(entity);
                }
                sdkEntityTriggerAreaComponent.TryClearEnteredAvatarsToBeProcessed();
            }
            else if (sdkEntityTriggerAreaComponent.ExitedEntitiesToBeProcessed.Count > 0)
            {
                if (activeAreas.Contains(entity))
                {
                    OnExitedCameraModeArea();
                    activeAreas.Remove(entity);
                }
                sdkEntityTriggerAreaComponent.TryClearExitedAvatarsToBeProcessed();
            }
        }

        [Query]
        [All(typeof(DeleteEntityIntention), typeof(PBCameraModeArea), typeof(CameraModeAreaComponent))]
        private void HandleEntityDestruction(Entity entity)
        {
            OnExitedCameraModeArea();
            activeAreas.Remove(entity);
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(PBCameraModeArea))]
        [All(typeof(CameraModeAreaComponent))]
        private void HandleComponentRemoval(Entity entity)
        {
            OnExitedCameraModeArea();
            activeAreas.Remove(entity);
            World.Remove<CameraModeAreaComponent>(entity);
        }

        internal void OnEnteredCameraModeArea(CameraMode targetCameraMode)
        {
            if (globalWorld.Has<InWorldCameraComponent>(cameraEntityProxy.Object))
                globalWorld.Add(cameraEntityProxy.Object, new ToggleInWorldCameraRequest { IsEnable = false, TargetCameraMode = targetCameraMode});

            ref CameraComponent camera = ref globalWorld.Get<CameraComponent>(cameraEntityProxy.Object!);

            cameraModeBeforeLastAreaEnter = camera.Mode;
            camera.Mode = targetCameraMode;
            camera.AddCameraInputLock();

            sceneRestrictionBusController.PushSceneRestriction(SceneRestriction.CreateCameraLocked(SceneRestrictionsAction.APPLIED));
        }

        internal void OnExitedCameraModeArea()
        {
            ref CameraComponent camera = ref globalWorld.Get<CameraComponent>(cameraEntityProxy.Object!);

            camera.RemoveCameraInputLock();

            // If there are more locks then there is another newer camera mode area in place
            if (camera.CameraInputChangeEnabled)
                camera.Mode = cameraModeBeforeLastAreaEnter == CameraMode.InWorld? CameraMode.ThirdPerson : cameraModeBeforeLastAreaEnter;

            sceneRestrictionBusController.PushSceneRestriction(SceneRestriction.CreateCameraLocked(SceneRestrictionsAction.REMOVED));
        }

        [Query]
        [All(typeof(CameraModeAreaComponent))]
        private void FinalizeComponents(Entity entity)
        {
            if (activeAreas.Remove(entity))
                OnExitedCameraModeArea();

            World.Remove<CameraModeAreaComponent>(entity);
        }

        public void FinalizeComponents(in Query query)
        {
            FinalizeComponentsQuery(World);
        }
    }
}
