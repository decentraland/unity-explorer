using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.Diagnostics;
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
    public partial class CaptureScreenshotSystem : BaseUnityLoopSystem
    {
        private readonly ScreenRecorder recorder;
        private readonly ScreenshotMetadataBuilder metadataBuilder;
        private readonly ScreenshotHudView hud;
        private readonly DCLInput.InWorldCameraActions inputSchema;

        private readonly ICoroutineRunner coroutineRunner;
        private readonly CancellationTokenSource ctx;
        private readonly Entity playerEntity;

        private SingleInstanceEntity cameraEntity;

        public CaptureScreenshotSystem(World world, ScreenRecorder recorder, DCLInput.InWorldCameraActions inputSchema, ScreenshotHudView hud,
            Entity playerEntity, ScreenshotMetadataBuilder metadataBuilder, ICoroutineRunner coroutineRunner)
            : base(world)
        {
            this.recorder = recorder;
            this.inputSchema = inputSchema;
            this.hud = hud;
            this.playerEntity = playerEntity;
            this.metadataBuilder = metadataBuilder;
            this.coroutineRunner = coroutineRunner;

            ctx = new CancellationTokenSource();
        }

        public override void Initialize()
        {
            cameraEntity = World.CacheCamera();
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
                    hud.Canvas.enabled = true;
                }

                return;
            }

            if (recorder.State == RecordingState.IDLE && inputSchema.Screenshot.triggered && World.Has<IsInWorldCamera>(cameraEntity))
            {
                hud.Canvas.enabled = false;  // TODO (Vit): This is a temporary solution for debug puproses. Will be replaced by proper MVC
                coroutineRunner.StartCoroutine(recorder.CaptureScreenshot());
                CollectMetadata();
            }
        }

        private void CollectMetadata()
        {
            metadataBuilder.Init(
                sceneParcel: World.Get<CharacterTransform>(playerEntity).Position.ToParcel(),
                frustumPlanes: GeometryUtility.CalculateFrustumPlanes(cameraEntity.GetCameraComponent(World).Camera)
            );

            AddPeopleInFrameToMetadataQuery(World);
            metadataBuilder.Build(ctx.Token).Forget();
        }

        [Query]
        private void AddPeopleInFrameToMetadata(Profile profile, RemoteAvatarCollider avatarCollider)
        {
            metadataBuilder.AddProfile(profile, avatarCollider.Collider);
        }
    }
}
