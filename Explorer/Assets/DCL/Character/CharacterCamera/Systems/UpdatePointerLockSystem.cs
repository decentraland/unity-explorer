using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Character.CharacterCamera.Components;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Groups;

namespace DCL.CharacterCamera.Systems
{
    [UpdateInGroup(typeof(SyncedPresentationSystemGroup))]
    public partial class UpdatePointerLockSystem : BaseUnityLoopSystem
    {
        private readonly World globalWorld;
        private readonly IExposedCameraData cameraData;

        public UpdatePointerLockSystem(
            World sceneWorld,
            World globalWorld,
            IExposedCameraData cameraData) : base(sceneWorld)
        {
            this.globalWorld = globalWorld;
            this.cameraData = cameraData;
        }

        protected override void Update(float t)
        {
            UpdateLockFromSceneQuery(World);
        }

        [Query]
        private void UpdateLockFromScene(ref PBPointerLock sdkPointerLock)
        {
            if (!sdkPointerLock.IsDirty) return;

            ref CursorComponent cursorComponent = ref globalWorld.TryGetRef<CursorComponent>(cameraData.CameraEntityProxy.Object, out bool exists);

            if (!exists) return;

            cursorComponent.CursorState = sdkPointerLock.IsPointerLocked ? CursorState.Locked : CursorState.Free;

            sdkPointerLock.IsDirty = false;
        }
    }
}
