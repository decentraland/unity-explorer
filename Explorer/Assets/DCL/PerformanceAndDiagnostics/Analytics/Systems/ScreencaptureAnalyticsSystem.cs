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

namespace DCL.Analytics.Systems
{
    [UpdateInGroup(typeof(CameraGroup))]
    [UpdateAfter(typeof(CaptureScreenshotSystem))]
    [LogCategory(ReportCategory.IN_WORLD_CAMERA)]
    public partial class ScreencaptureAnalyticsSystem : BaseUnityLoopSystem
    {
        private static readonly StringBuilder ADDRESS_BUILDER = new (256);

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

        public override void Dispose()
        {
            storage.ScreenshotUploaded -= OnScreenshotUploaded;
            base.Dispose();
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

        private string GetVisiblePeopleAddresses(ReadOnlySpan<VisiblePerson> persons)
        {
            ADDRESS_BUILDER.Clear();

            for (var i = 0; i < persons.Length; i++)
            {
                ADDRESS_BUILDER.Append(persons[i].userAddress);
                if (i < persons.Length - 1)
                    ADDRESS_BUILDER.Append(',');
            }

            return ADDRESS_BUILDER.ToString();
        }

        protected override void Update(float t)
        {
            if (World.TryGet(camera, out ToggleInWorldCameraRequest request))
            {
                if (request.IsEnable)
                    analytics.Track(AnalyticsEvents.CameraReel.CAMERA_OPEN, new JsonObject
                    {
                        { "source", request.Source },
                    });

                World.Remove<ToggleInWorldCameraRequest>(camera);
            }
        }
    }
}
