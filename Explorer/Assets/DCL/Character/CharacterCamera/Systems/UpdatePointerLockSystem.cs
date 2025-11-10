using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
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
        private readonly Entity cameraEntity;

        public UpdatePointerLockSystem(
            World sceneWorld,
            World globalWorld,
            IExposedCameraData cameraData,
            Entity cameraEntity) : base(sceneWorld)
        {
            this.globalWorld = globalWorld;
            this.cameraData = cameraData;
            this.cameraEntity = cameraEntity;
        }

        protected override void Update(float t)
        {
            ref PBPointerLock sdkPointerLock = ref World.TryGetRef<PBPointerLock>(cameraEntity, out bool exists);

            if (exists)
                UpdateLockFromScene(ref sdkPointerLock);
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
