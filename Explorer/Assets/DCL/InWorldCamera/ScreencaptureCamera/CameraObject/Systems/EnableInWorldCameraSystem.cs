using Arch.Core;
using Arch.SystemGroups;
using DCL.CharacterCamera;
using DCL.CharacterCamera.Systems;
using DCL.Diagnostics;
using DCL.InWorldCamera.ScreencaptureCamera.UI;
using ECS.Abstract;
using UnityEngine;

namespace DCL.InWorldCamera.ScreencaptureCamera.CameraObject.Systems
{
    [UpdateInGroup(typeof(CameraGroup))]
    [UpdateAfter(typeof(ApplyCinemachineCameraInputSystem))]
    [LogCategory(ReportCategory.IN_WORLD_CAMERA)]
    public partial class EnableInWorldCameraSystem : BaseUnityLoopSystem
    {
        private readonly DCLInput.InWorldCameraActions inputSchema;
        private readonly GameObject hudPrefab;

        private SingleInstanceEntity camera;

        private bool isInstantiated;
        private ScreenshotHudView hud;
        private ScreenRecorder recorder;

        public EnableInWorldCameraSystem(World world, DCLInput.InWorldCameraActions inputSchema, GameObject hudPrefab) : base(world)
        {
            this.inputSchema = inputSchema;
            this.hudPrefab = hudPrefab;
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
            ref CameraComponent cameraComponent = ref World.Get<CameraComponent>(camera);
            cameraComponent.Mode = CameraMode.InWorld;

            if (isInstantiated)
                hud.gameObject.SetActive(true);
            else
            {
                hud = Object.Instantiate(hudPrefab, Vector3.zero, Quaternion.identity).GetComponent<ScreenshotHudView>();
                recorder = new ScreenRecorder(hud.GetComponent<RectTransform>());
                isInstantiated = true;
            }

            World.Add<IsInWorldCamera>(camera);
        }

        private void DisableCamera()
        {
            ref CameraComponent cameraComponent = ref World.Get<CameraComponent>(camera);
            cameraComponent.Mode = CameraMode.ThirdPerson;

            hud.gameObject.SetActive(false);

            World.Remove<IsInWorldCamera>(camera);
        }
    }
}
