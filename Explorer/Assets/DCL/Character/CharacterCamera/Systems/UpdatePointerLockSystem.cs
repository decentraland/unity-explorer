using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Character.CharacterCamera.Components;
using DCL.ECSComponents;
using DCL.Input.Component;
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

            globalWorld.AddOrGet(cameraData.CameraEntityProxy.Object, new PointerLockIntention(sdkPointerLock.IsPointerLocked));

            sdkPointerLock.IsDirty = false;
        }
    }
}
