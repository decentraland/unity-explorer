using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.Diagnostics;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DCL.InWorldCamera.UI;
using DCL.Multiplayer.Profiles.Entities;
using DCL.Profiles;
using ECS.Abstract;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.InWorldCamera.Systems
{
    [UpdateInGroup(typeof(CameraGroup))]
    [UpdateAfter(typeof(ToggleInWorldCameraActivitySystem))]
    [LogCategory(ReportCategory.IN_WORLD_CAMERA)]
    public sealed partial class CaptureScreenshotSystem : BaseUnityLoopSystem
    {
        private const float SPLASH_FX_DURATION = 0.5f;
        private const float MIDDLE_PAUSE_FX_DURATION = 0.1f;
        private const float IMAGE_TRANSITION_FX_DURATION = 0.5f;

        private readonly ScreenRecorder recorder;
        private readonly ScreenshotMetadataBuilder metadataBuilder;

        private readonly ICoroutineRunner coroutineRunner;
        private readonly ICameraReelStorageService cameraReelStorageService;
        private readonly InWorldCameraController hudController;
        private readonly CancellationTokenSource ctx;
        private readonly Entity playerEntity;

        private SingleInstanceEntity camera;
        private Texture2D? screenshot;
        private ScreenshotMetadata? metadata;
        private string currentSource;

        public CaptureScreenshotSystem(
            World world,
            ScreenRecorder recorder,
            Entity playerEntity,
            ScreenshotMetadataBuilder metadataBuilder,
            ICoroutineRunner coroutineRunner,
            ICameraReelStorageService cameraReelStorageService,
            InWorldCameraController hudController)
            : base(world)
        {
            this.recorder = recorder;
            this.playerEntity = playerEntity;
            this.metadataBuilder = metadataBuilder;
            this.coroutineRunner = coroutineRunner;
            this.cameraReelStorageService = cameraReelStorageService;
            this.hudController = hudController;

            ctx = new CancellationTokenSource();
        }

        public override void Initialize()
        {
            camera = World.CacheCamera();
        }

        protected override void OnDispose()
        {
            ctx.SafeCancelAndDispose();
        }

        protected override void Update(float t)
        {
            if (recorder.State == RecordingState.CAPTURING || hudController.IsVfxInProgress)
                return;

            if (recorder.State == RecordingState.SCREENSHOT_READY && metadataBuilder.MetadataIsReady)
            {
                ProcessCapturedScreenshot();
                return;
            }

            if (ScreenshotIsRequested() && cameraReelStorageService.StorageStatus.HasFreeSpace)
            {
                hudController.SetViewCanvasActive(false);
                coroutineRunner.StartCoroutine(recorder.CaptureScreenshot());
                CollectMetadata();
            }
        }

        private void ProcessCapturedScreenshot()
        {
            screenshot = recorder.GetScreenshotAndReset();
            metadata = metadataBuilder.GetMetadataAndReset();

            try
            {
                cameraReelStorageService.UploadScreenshotAsync(screenshot, metadata, currentSource, ctx.Token).Forget();

                hudController.SetViewCanvasActive(true);
                hudController.PlayScreenshotFX(screenshot, SPLASH_FX_DURATION, MIDDLE_PAUSE_FX_DURATION, IMAGE_TRANSITION_FX_DURATION);
                hudController.DebugCapture(screenshot, metadata);
            }
            catch (OperationCanceledException) { }
            catch (ScreenshotLimitReachedException) { hudController.Show(); }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.CAMERA_REEL); }
        }

        private bool ScreenshotIsRequested()
        {
            if (recorder.State != RecordingState.IDLE) return false;

            if (World.TryGet(camera, out TakeScreenshotRequest request))
            {
                currentSource = request.Source;
                World.Remove<TakeScreenshotRequest>(camera);
                return true;
            }

            return false;
        }

        private void CollectMetadata()
        {
            GetScaledFrustumPlanes(camera.GetCameraComponent(World).Camera, ScreenRecorder.FRAME_SCALE, out Plane[]? frustumPlanes);

            metadataBuilder.Init(sceneParcel: World.Get<CharacterTransform>(playerEntity).Position.ToParcel(), frustumPlanes);

            AddPeopleInFrameToMetadataQuery(World);
            metadataBuilder.BuildAsync(ctx.Token).Forget();
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
