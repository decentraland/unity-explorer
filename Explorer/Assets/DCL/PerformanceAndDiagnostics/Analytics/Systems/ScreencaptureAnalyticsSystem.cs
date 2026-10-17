using Arch.Core;
using Arch.SystemGroups;
using DCL.CharacterCamera;
using DCL.Diagnostics;
using DCL.InWorldCamera;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DCL.InWorldCamera.Systems;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.Utilities.Extensions;
using ECS.Abstract;
using Newtonsoft.Json.Linq;
using System;
using UnityEngine.Pool;

namespace DCL.Analytics.Systems
{
    [UpdateInGroup(typeof(CameraGroup))]
    [UpdateBefore(typeof(ToggleInWorldCameraActivitySystem))]
    [UpdateBefore(typeof(CleanupScreencaptureCameraSystem))]
    [LogCategory(ReportCategory.IN_WORLD_CAMERA)]
    public partial class ScreencaptureAnalyticsSystem : BaseUnityLoopSystem
    {
        private static readonly JArray ADDRESS_BUILDER = new ();
        private static readonly ObjectPool<JObject> JSON_OBJECT_POOL = new (
            createFunc: () => new JObject(),
            actionOnRelease: obj => obj.RemoveAll());

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
            analytics.Track(AnalyticsEvents.CameraReel.TAKE_PHOTO, new JObject
            {
                { "Photo UUID", response.id },
                { "Profiles Enhanced", GetVisiblePeopleAddresses(response.metadata.visiblePeople) },
                { "source", source },
            });
        }

        private JArray GetVisiblePeopleAddresses(ReadOnlySpan<VisiblePerson> persons)
        {
            foreach (var element in ADDRESS_BUILDER)
                if (element is JObject jObject)
                    JSON_OBJECT_POOL.Release(jObject);

            ADDRESS_BUILDER.Clear();

            foreach (var visiblePerson in persons)
            {
                JObject JObject = JSON_OBJECT_POOL.Get().EnsureNotNull();
                JObject.Add("address", visiblePerson.userAddress);
                JObject.Add("isEmoting", visiblePerson.isEmoting);

                ADDRESS_BUILDER.Add(JObject);
            }

            return ADDRESS_BUILDER;
        }

        protected override void Update(float t)
        {
            if (World.TryGet(camera, out ToggleInWorldCameraRequest request) && request.IsEnable)
                analytics.Track(AnalyticsEvents.CameraReel.CAMERA_OPEN, new JObject
                {
                    { "source", request.Source },
                });
        }
    }
}
