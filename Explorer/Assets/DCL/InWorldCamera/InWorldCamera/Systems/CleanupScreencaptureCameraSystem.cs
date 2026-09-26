using Arch.Core;
using Arch.SystemGroups;
using DCL.CharacterCamera;
using DCL.Diagnostics;
using ECS.Abstract;

namespace DCL.InWorldCamera.Systems
{
    [UpdateInGroup(typeof(CameraGroup))]
    [UpdateAfter(typeof(CaptureScreenshotSystem))]
    [LogCategory(ReportCategory.IN_WORLD_CAMERA)]
    public partial class CleanupScreencaptureCameraSystem : BaseUnityLoopSystem
    {
        private SingleInstanceEntity camera;

        private CleanupScreencaptureCameraSystem(World world) : base(world) { }

        public override void Initialize()
        {
            camera = World.CacheCamera();
        }

        protected override void Update(float t)
        {
            if (World.Has<ToggleInWorldCameraRequest>(camera))
                World.Remove<ToggleInWorldCameraRequest>(camera);
        }
    }
}
