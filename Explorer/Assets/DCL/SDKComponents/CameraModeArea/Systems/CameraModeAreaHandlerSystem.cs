using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.CharacterCamera;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.MainPlayerTriggerArea;
using DCL.SDKComponents.CameraModeArea.Components;
using DCL.Utilities;
using ECS.Abstract;
using ECS.LifeCycle;
using ECS.Unity.Groups;
using ECS.Unity.Transforms.Components;
using System;
using UnityEngine;
using CameraType = DCL.ECSComponents.CameraType;

namespace DCL.SDKComponents.CameraModeArea.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [UpdateBefore(typeof(MainPlayerTriggerAreaHandlerSystem))]
    [LogCategory(ReportCategory.CAMERA_MODE_AREA)]
    [ThrottlingEnabled]
    public partial class CameraModeAreaHandlerSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly World globalWorld;
        private Entity cameraEntity;

        // private CameraComponent cameraComponent;
        // private ref CameraComponent cameraComponent => ref globalWorld.Get<CameraComponent>(cameraEntity);

        public CameraModeAreaHandlerSystem(World world, WorldProxy globalWorldProxy) : base(world)
        {
            globalWorld = globalWorldProxy.GetWorld();

            // TODO: Propagate the CameraEntity from the global CharacterCameraPlugin somehow...
            // cameraComponent = globalWorldProxy.GetWorld().Get<CameraComponent>(cameraEntity);
            globalWorld!.Query(new QueryDescription().WithAll<CameraComponent>(), entity => { cameraEntity = entity; });
        }

        protected override void Update(float t)
        {
            // TODO: Check if we have control of the camera mode as well?

            // UpdateCameraModeAreaQuery(World);
            SetupCameraModeAreaQuery(World);
        }

        [Query]
        [None(typeof(MainPlayerTriggerAreaComponent))]
        [All(typeof(TransformComponent))]
        private void SetupCameraModeArea(in Entity entity, ref PBCameraModeArea pbCameraModeArea)
        {
            var targetCameraMode = (CameraMode)pbCameraModeArea.Mode;
            CameraModeAreaComponent cameraModeAreaComponent = new ();

            World.Add(entity, new MainPlayerTriggerAreaComponent
            {
                areaSize = pbCameraModeArea.Area,
                OnEnteredTrigger = () => OnEnteredCameraModeArea(targetCameraMode, ref cameraModeAreaComponent),
                OnExitedTrigger = () => OnExitedCameraModeArea(ref cameraModeAreaComponent),
                IsDirty = true,
            }, cameraModeAreaComponent);
        }

        /*[Query]
        [All(typeof(TransformComponent))]
        private void UpdateCameraModeArea(ref PBCameraModeArea pbCameraModeArea, ref MainPlayerTriggerAreaComponent mainPlayerTriggerAreaComponent)
        {
            if (!pbCameraModeArea.IsDirty) return;

            Debug.Log($"PRAVS - Update CAMERA MODE AREA SIZE from {mainPlayerTriggerAreaComponent.areaSize} to {pbCameraModeArea.Area}");

            // TODO: Support changing CameraType-Mode (actions) as well?
            mainPlayerTriggerAreaComponent.areaSize = pbCameraModeArea.Area;
            mainPlayerTriggerAreaComponent.IsDirty = true;
        }*/

        /*private void UpdateCameraMode(CameraMode targetCameraMode, bool lockMode)
        {
            // ref CameraComponent camera = ref globalWorld.Get<CameraComponent>(cameraEntity);
            ref CameraComponent camera = ref cameraComponent;
            camera.Mode = targetCameraMode;
            camera.LockCameraInput = lockMode;

            Debug.Log($"PRAVS - CHANGE CAMERA MODE TO {targetCameraMode}");
        }*/
        private void OnEnteredCameraModeArea(CameraMode targetCameraMode, ref CameraModeAreaComponent cameraModeAreaComponent)
        {
            ref CameraComponent camera = ref globalWorld.Get<CameraComponent>(cameraEntity);
            cameraModeAreaComponent.modeBeforeEntering = camera.Mode;
            camera.Mode = targetCameraMode;
            camera.LockCameraInput = true;

            Debug.Log($"PRAVS - CHANGE CAMERA MODE TO {camera.Mode}");
        }

        private void OnExitedCameraModeArea(ref CameraModeAreaComponent cameraModeAreaComponent)
        {
            ref CameraComponent camera = ref globalWorld.Get<CameraComponent>(cameraEntity);
            camera.Mode = cameraModeAreaComponent.modeBeforeEntering;
            camera.LockCameraInput = false;

            Debug.Log($"PRAVS - CHANGE CAMERA MODE TO {camera.Mode}");
        }

        public void FinalizeComponents(in Query query)
        {
            throw new NotImplementedException();
        }
    }
}
