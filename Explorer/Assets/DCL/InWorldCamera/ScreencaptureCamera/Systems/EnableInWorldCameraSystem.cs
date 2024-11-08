using Arch.Core;
using Arch.SystemGroups;
using DCL.CharacterCamera;
using DCL.CharacterCamera.Systems;
using DCL.Diagnostics;
using ECS.Abstract;
using UnityEngine;

namespace DCL.InWorldCamera.ScreencaptureCamera.Systems
{
    [UpdateInGroup(typeof(CameraGroup))]
    [UpdateAfter(typeof(ApplyCinemachineCameraInputSystem))]
    [LogCategory(ReportCategory.IN_WORLD_CAMERA)]
    public partial class EnableInWorldCameraSystem : BaseUnityLoopSystem
    {
        private readonly DCLInput.InWorldCameraActions inputSchema;
        private readonly GameObject hud;

        private SingleInstanceEntity camera;

        public EnableInWorldCameraSystem(World world, DCLInput.InWorldCameraActions inputSchema, GameObject hud) : base(world)
        {
            this.inputSchema = inputSchema;
            this.hud = hud;
        }

        public override void Initialize()
        {
            camera = World.CacheCamera();
        }

        protected override void Update(float t)
        {
            if (inputSchema.ToggleInWorld!.triggered)
            {
                if (World.Has<IsInWorldCamera>(camera))
                    DisableCamera();
                else
                    EnableCamera();
            }
        }

        private void EnableCamera()
        {
            hud.SetActive(true);
            World.Add<IsInWorldCamera>(camera);
        }

        private void DisableCamera()
        {
            hud.SetActive(false);
            World.Remove<IsInWorldCamera>(camera);
        }
    }
}
