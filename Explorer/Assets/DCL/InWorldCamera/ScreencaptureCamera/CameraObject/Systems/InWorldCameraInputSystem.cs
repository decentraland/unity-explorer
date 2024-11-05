using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.CharacterCamera;
using DCL.CharacterCamera.Systems;
using DCL.Diagnostics;
using DCL.InWorldCamera.ScreencaptureCamera.UI;
using ECS.Abstract;
using System;
using UnityEngine;
using static DCL.InWorldCamera.ScreencaptureCamera.CameraObject.InWorldCameraComponents;
using Object = UnityEngine.Object;

namespace DCL.InWorldCamera.ScreencaptureCamera.CameraObject.Systems
{
    [UpdateInGroup(typeof(CameraGroup))]
    [UpdateAfter(typeof(ApplyCinemachineCameraInputSystem))]
    [LogCategory(ReportCategory.IN_WORLD_CAMERA)]
    public partial class InWorldCameraInputSystem : BaseUnityLoopSystem
    {
        private readonly DCLInput.InWorldCameraActions inputSchema;

        private readonly GameObject hudPrefab;
        private ScreenRecorder recorder;

        private bool isInstantiated;
        private ScreenshotHudView hud;

        public InWorldCameraInputSystem(World world, DCLInput.InWorldCameraActions inputSchema, GameObject hudPrefab) : base(world)
        {
            this.inputSchema = inputSchema;
            this.hudPrefab = hudPrefab;
        }

        protected override void Update(float t)
        {
            EmitInputQuery(World);
        }

        [Query]
        [All(typeof(IsInWorldCamera))]
        private void EmitInput(in Entity entity)
        {
            if (!isInstantiated)
            {
                hud = Object.Instantiate(hudPrefab, Vector3.zero, Quaternion.identity).GetComponent<ScreenshotHudView>();
                recorder = new ScreenRecorder(hud.GetComponent<RectTransform>());

                isInstantiated = true;
            }

            if (isInstantiated && inputSchema.Screenshot.triggered)
            {
                hud.GetComponent<Canvas>().enabled = false;
                hud.StartCoroutine(recorder.CaptureScreenshot(Show));
            }
        }

        private void Show(Texture2D screenshot)
        {
            hud.Screenshot = screenshot;
            hud.GetComponent<Canvas>().enabled = true;
        }
    }
}
