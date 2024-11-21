using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.Diagnostics;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.InWorldCamera.ScreencaptureCamera.UI;
using DCL.Multiplayer.Profiles.Entities;
using DCL.Profiles;
using ECS.Abstract;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.InWorldCamera.ScreencaptureCamera.Systems
{
    [UpdateInGroup(typeof(CameraGroup))]
    [UpdateAfter(typeof(ToggleInWorldCameraActivitySystem))]
    [LogCategory(ReportCategory.IN_WORLD_CAMERA)]
    public sealed partial class CaptureScreenshotSystem : BaseUnityLoopSystem
    {
        private readonly ScreenRecorder recorder;
        private readonly ScreenshotMetadataBuilder metadataBuilder;
        private readonly ScreenshotHudView hud;

        private readonly ICoroutineRunner coroutineRunner;
        private readonly ICameraReelStorageService cameraReelStorageService;
        private readonly CancellationTokenSource ctx;
        private readonly Entity playerEntity;

        private SingleInstanceEntity camera;

        public CaptureScreenshotSystem(World world, ScreenRecorder recorder,
            ScreenshotHudView hud, Entity playerEntity, ScreenshotMetadataBuilder metadataBuilder, ICoroutineRunner coroutineRunner,
            ICameraReelStorageService cameraReelStorageService)
            : base(world)
        {
            this.recorder = recorder;
            this.hud = hud;
            this.playerEntity = playerEntity;
            this.metadataBuilder = metadataBuilder;
            this.coroutineRunner = coroutineRunner;
            this.cameraReelStorageService = cameraReelStorageService;

            ctx = new CancellationTokenSource();
        }

        public override void Initialize()
        {
            camera = World.CacheCamera();
        }

        public override void Dispose()
        {
            ctx.SafeCancelAndDispose();
        }

        protected override void Update(float t)
        {
            if (recorder.State == RecordingState.CAPTURING)
                return;

            if (recorder.State == RecordingState.SCREENSHOT_READY && metadataBuilder.MetadataIsReady)
            {
                // TODO (Vit): This is a temporary solution for debug purposes. Will be replaced by proper MVC + sending to backend
                {
                    hud.Screenshot = recorder.GetScreenshotAndReset();
                    hud.Metadata = metadataBuilder.GetMetadataAndReset();

                    cameraReelStorageService.UploadScreenshotAsync(hud.Screenshot, hud.Metadata, ctx.Token).Forget();

                    hud.Canvas.enabled = true;
                }

                return;
            }

            if (recorder.State == RecordingState.IDLE && World.TryGet<InWorldCameraInput>(camera, out var input) && input.TakeScreenshot)
            {
                hud.Canvas.enabled = false;  // TODO (Vit): This is a temporary solution for debug puproses. Will be replaced by proper MVC
                coroutineRunner.StartCoroutine(recorder.CaptureScreenshot());
                CollectMetadata();
            }
        }

        private void CollectMetadata()
        {
            GetScaledFrustumPlanes(camera.GetCameraComponent(World).Camera, ScreenRecorder.FRAME_SCALE, out var frustumPlanes);

            metadataBuilder.Init(sceneParcel: World.Get<CharacterTransform>(playerEntity).Position.ToParcel(), frustumPlanes);

            AddPeopleInFrameToMetadataQuery(World);
            metadataBuilder.Build(ctx.Token).Forget();
        }

        [Query]
        private void AddPeopleInFrameToMetadata(Profile profile, RemoteAvatarCollider avatarCollider)
        {
            metadataBuilder.AddProfile(profile, avatarCollider.Collider);
        }

        private static void GetScaledFrustumPlanes(Camera camera, float scaleFactor, out Plane[] frustumPlanes)
        {
            float originalFOV = camera.fieldOfView;
            float originalAspect = camera.aspect;

            // Calculate new FOV and aspect ratio for the scaled view
            camera.fieldOfView = Mathf.Atan(Mathf.Tan(originalFOV * Mathf.Deg2Rad / 2f) * scaleFactor) * 2f * Mathf.Rad2Deg;
            camera.aspect = originalAspect; // Maintain the same aspect ratio since we're scaling uniformly

            // Get the scaled frustum planes
            frustumPlanes = GeometryUtility.CalculateFrustumPlanes(camera);

            // Restore original camera settings
            camera.fieldOfView = originalFOV;
            camera.aspect = originalAspect;
        }
    }
}
