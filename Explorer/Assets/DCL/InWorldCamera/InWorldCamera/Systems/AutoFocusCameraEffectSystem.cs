using Arch.Core;
using Arch.SystemGroups;
using DCL.CharacterCamera;
using DCL.Diagnostics;
using DCL.InWorldCamera.UI;
using ECS.Abstract;
using UnityEngine;

namespace DCL.InWorldCamera.Systems
{
    [UpdateInGroup(typeof(CameraGroup))]
    [LogCategory(ReportCategory.IN_WORLD_CAMERA)]
    [UpdateAfter(typeof(CaptureScreenshotSystem))]
    public partial class AutoFocusCameraEffectSystem : BaseUnityLoopSystem
    {
        private const float SCREEN_AXIS_CENTER = 0.5f;

        private readonly InWorldCameraEffectsController controller;
        private readonly LayerMask autofocusLayers = -1;
        private readonly float autofocusUpdateRate = 4;
        private readonly float autofocusBlendSpeed = 5f;
        private readonly float autofocusMaxDistance = 500f;

        private float targetFocusDistance;
        private float autofocusTimer;
        private bool hasValidFocusTarget;

        private SingleInstanceEntity cameraEntity;

        private Camera? cameraCached;
        private Camera camera => cameraCached ??= cameraEntity.GetCameraComponent(World).Camera;

        public AutoFocusCameraEffectSystem(World world, InWorldCameraEffectsController controller) : base(world)
        {
            this.controller = controller;
            targetFocusDistance = this.controller.FocusDistance;
        }

        public override void Initialize()
        {
            cameraEntity = World.CacheCamera();
        }

        protected override void Update(float t)
        {
            if (controller.DofEnabled && controller.AutoFocusEnabled)
            {
                autofocusTimer += t;

                if (autofocusTimer >= 1f / autofocusUpdateRate)
                {
                    autofocusTimer = 0f;

                    Ray ray = camera.ViewportPointToRay(new Vector3(SCREEN_AXIS_CENTER, SCREEN_AXIS_CENTER, 0f));

                    if (Physics.Raycast(ray, out RaycastHit hit, autofocusMaxDistance, autofocusLayers))
                        targetFocusDistance = hit.distance;
                }

                if (Mathf.Abs(controller.FocusDistance - targetFocusDistance) > 0.01f)
                    controller.SetAutoFocus(Mathf.Lerp(controller.FocusDistance, targetFocusDistance, t * autofocusBlendSpeed), targetFocusDistance);
            }
        }
    }
}
