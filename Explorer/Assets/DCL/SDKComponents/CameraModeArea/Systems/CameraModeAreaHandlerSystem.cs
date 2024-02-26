using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.CharacterCamera;
using DCL.CharacterTriggerArea.Components;
using DCL.CharacterTriggerArea.Systems;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Utilities;
using ECS.Abstract;
using ECS.Unity.Transforms.Components;

namespace DCL.SDKComponents.CameraModeArea.Systems
{
    [UpdateInGroup(typeof(PostPhysicsSystemGroup))]
    [UpdateBefore(typeof(CharacterTriggerAreaCleanupSystem))]
    [LogCategory(ReportCategory.CAMERA_MODE_AREA)]
    public partial class CameraModeAreaHandlerSystem : BaseUnityLoopSystem
    {
        private static CameraMode cameraModeBeforeLastAreaEnter; // There's only 1 camera at a time

        private readonly World globalWorld;
        private readonly EntityProxy cameraEntityProxy;

        public CameraModeAreaHandlerSystem(World world, WorldProxy globalWorldProxy, EntityProxy cameraEntityProxy) : base(world)
        {
            globalWorld = globalWorldProxy.World;
            this.cameraEntityProxy = cameraEntityProxy;
        }

        protected override void Update(float t)
        {
            if (!cameraEntityProxy.Entity.HasValue) return;

            UpdateCameraModeAreaQuery(World);
            SetupCameraModeAreaQuery(World);
        }

        [Query]
        [None(typeof(CharacterTriggerAreaComponent))]
        [All(typeof(TransformComponent))]
        private void SetupCameraModeArea(in Entity entity, ref PBCameraModeArea pbCameraModeArea)
        {
            World.Add(entity, new CharacterTriggerAreaComponent(areaSize: pbCameraModeArea.Area, targetOnlyMainPlayer: true));
        }

        [Query]
        [All(typeof(TransformComponent))]
        private void UpdateCameraModeArea(ref PBCameraModeArea pbCameraModeArea, ref CharacterTriggerAreaComponent characterTriggerAreaComponent)
        {
            if (characterTriggerAreaComponent.EnteredThisFrame!.Count > 0) { OnEnteredCameraModeArea((CameraMode)pbCameraModeArea.Mode); }
            else if (characterTriggerAreaComponent.ExitedThisFrame!.Count > 0) { OnExitedCameraModeArea(); }

            if (pbCameraModeArea.IsDirty)
            {
                characterTriggerAreaComponent.AreaSize = pbCameraModeArea.Area;
                characterTriggerAreaComponent.IsDirty = true;
            }
        }

        internal void OnEnteredCameraModeArea(CameraMode targetCameraMode)
        {
            ref CameraComponent camera = ref globalWorld.Get<CameraComponent>(cameraEntityProxy.Entity!.Value);
            cameraModeBeforeLastAreaEnter = camera.Mode;
            camera.Mode = targetCameraMode;
            camera.AddCameraInputLock();
        }

        internal void OnExitedCameraModeArea()
        {
            ref CameraComponent camera = ref globalWorld.Get<CameraComponent>(cameraEntityProxy.Entity!.Value);

            camera.RemoveCameraInputLock();

            // If there are more locks then there is another newer camera mode area in place
            if (camera.CameraInputLocks == 0)
                camera.Mode = cameraModeBeforeLastAreaEnter;
        }
    }
}
