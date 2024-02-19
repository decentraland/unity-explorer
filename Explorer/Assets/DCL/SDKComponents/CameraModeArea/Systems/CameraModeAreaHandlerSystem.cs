using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.CharacterCamera;
using DCL.CharacterTriggerArea.Components;
using DCL.CharacterTriggerArea.Systems;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Utilities;
using ECS.Abstract;
using ECS.LifeCycle;
using ECS.Unity.Groups;
using ECS.Unity.Transforms.Components;
using System;
using UnityEngine;

namespace DCL.SDKComponents.CameraModeArea.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [UpdateBefore(typeof(CharacterTriggerAreaHandlerSystem))]
    [LogCategory(ReportCategory.CAMERA_MODE_AREA)]
    [ThrottlingEnabled]
    public partial class CameraModeAreaHandlerSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private static CameraMode cameraModeBeforeLastAreaEnter; // There's only 1 camera at a time
        private readonly World globalWorld;
        private readonly Entity cameraEntity;

        public CameraModeAreaHandlerSystem(World world, WorldProxy globalWorldProxy, EntityProxy cameraEntityProxy) : base(world)
        {
            globalWorld = globalWorldProxy.World;
            cameraEntity = cameraEntityProxy.Entity!.Value;
        }

        protected override void Update(float t)
        {
            UpdateCameraModeAreaQuery(World);
            SetupCameraModeAreaQuery(World);
        }

        [Query]
        [None(typeof(CharacterTriggerAreaComponent))]
        [All(typeof(TransformComponent))]
        private void SetupCameraModeArea(in Entity entity, ref PBCameraModeArea pbCameraModeArea)
        {
            var targetCameraMode = (CameraMode)pbCameraModeArea.Mode;

            World.Add(entity, new CharacterTriggerAreaComponent
            {
                areaSize = pbCameraModeArea.Area,
                targetOnlyMainPlayer = true,
                OnEnteredTrigger = avatarCo => OnEnteredCameraModeArea(targetCameraMode),
                OnExitedTrigger = OnExitedCameraModeArea,
                IsDirty = true,
            });
        }

        [Query]
        [All(typeof(TransformComponent))]
        private void UpdateCameraModeArea(ref PBCameraModeArea pbCameraModeArea, ref CharacterTriggerAreaComponent characterTriggerAreaComponent)
        {
            if (!pbCameraModeArea.IsDirty) return;

            var targetCameraMode = (CameraMode)pbCameraModeArea.Mode;
            characterTriggerAreaComponent.OnEnteredTrigger = avatarCollider => OnEnteredCameraModeArea(targetCameraMode);
            characterTriggerAreaComponent.OnExitedTrigger = OnExitedCameraModeArea;
            characterTriggerAreaComponent.areaSize = pbCameraModeArea.Area;
            characterTriggerAreaComponent.IsDirty = true;
        }

        private void OnEnteredCameraModeArea(CameraMode targetCameraMode)
        {
            ref CameraComponent camera = ref globalWorld.Get<CameraComponent>(cameraEntity);
            cameraModeBeforeLastAreaEnter = camera.Mode;
            camera.Mode = targetCameraMode;
            camera.AddCameraInputLock();
        }

        private void OnExitedCameraModeArea(Collider avatarCollider)
        {
            ref CameraComponent camera = ref globalWorld.Get<CameraComponent>(cameraEntity);

            camera.RemoveCameraInputLock();

            // If there are more locks then there is another newer camera mode area in place
            if (camera.CameraInputLocks == 0)
                camera.Mode = cameraModeBeforeLastAreaEnter;
        }

        public void FinalizeComponents(in Query query)
        {
            throw new NotImplementedException();
        }
    }
}
