using Arch.Core;
using Arch.SystemGroups;
using DCL.CharacterCamera;
using DCL.Diagnostics;
using DCL.InWorldCamera;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DCL.InWorldCamera.Systems;
using DCL.PerformanceAndDiagnostics.Analytics;
using ECS.Abstract;
using Segment.Serialization;
using System;
using System.Text;
using UnityEngine.Pool;

namespace DCL.Analytics.Systems
{
    [UpdateInGroup(typeof(CameraGroup))]
    [UpdateBefore(typeof(ToggleInWorldCameraActivitySystem))]
    [UpdateBefore(typeof(CleanupScreencaptureCameraSystem))]
    [LogCategory(ReportCategory.IN_WORLD_CAMERA)]
    public partial class ScreencaptureAnalyticsSystem : BaseUnityLoopSystem
    {
        private static readonly JsonArray ADDRESS_BUILDER = new ();
        private static readonly ObjectPool<JsonObject> JSON_OBJECT_POOL = new (
            createFunc: () => new JsonObject(),
            actionOnRelease: obj => obj.Clear());

        private readonly IAnalyticsController analytics;
        private readonly ICameraReelStorageService storage;

        private SingleInstanceEntity camera;

        private ScreencaptureAnalyticsSystem(World world, IAnalyticsController analytics, ICameraReelStorageService storage) : base(world)
        {
            this.analytics = analytics;
            this.storage = storage;

            storage.ScreenshotUploaded += OnScreenshotUploaded;
        }

        public override void Initialize()
        {
            camera = World.CacheCamera();
        }

        protected override void OnDispose()
        {
            storage.ScreenshotUploaded -= OnScreenshotUploaded;
        }

        private void OnScreenshotUploaded(CameraReelResponse response, CameraReelStorageStatus _, string source)
        {
            analytics.Track(AnalyticsEvents.CameraReel.TAKE_PHOTO, new JsonObject
            {
                { "Photo UUID", response.id },
                { "Profiles", GetVisiblePeopleAddresses(response.metadata.visiblePeople) },
                { "source", source },
            });
        }

        private JsonArray GetVisiblePeopleAddresses(ReadOnlySpan<VisiblePerson> persons)
        {
            foreach (JsonObject element in ADDRESS_BUILDER)
                JSON_OBJECT_POOL.Release(element);
            ADDRESS_BUILDER.Clear();

            foreach (var visiblePerson in persons)
            {
                JsonObject jsonObject = JSON_OBJECT_POOL.Get();
                jsonObject.Add("address", visiblePerson.userAddress);
                jsonObject.Add("isEmoting", visiblePerson.isEmoting);

                ADDRESS_BUILDER.Add(jsonObject);
            }

            return ADDRESS_BUILDER;
        }

        protected override void Update(float t)
        {
            if (World.TryGet(camera, out ToggleInWorldCameraRequest request) && request.IsEnable)
                analytics.Track(AnalyticsEvents.CameraReel.CAMERA_OPEN, new JsonObject
                {
                    { "source", request.Source },
                });
        }
    }
}
