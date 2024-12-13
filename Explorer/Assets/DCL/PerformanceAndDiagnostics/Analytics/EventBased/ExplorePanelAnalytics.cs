using DCL.ExplorePanel;
using DCL.InWorldCamera.CameraReelGallery;
using DCL.Navmap;
using Segment.Serialization;
using System;
using UnityEngine;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class ExplorePanelAnalytics : IDisposable
    {
        private readonly IAnalyticsController analytics;
        private readonly NavmapController navmapController;
        private readonly CameraReelController cameraReelController;

        public ExplorePanelAnalytics(IAnalyticsController analytics, ExplorePanelController controller)
        {
            this.analytics = analytics;
            this.navmapController = controller.NavmapController;
            this.cameraReelController = controller.CameraReelController;

            navmapController.FloatingPanelController.OnJumpIn += OnJumpIn;
            cameraReelController.Activated += TrackCameraReelOpen;
        }

        public void Dispose()
        {
            navmapController.FloatingPanelController.OnJumpIn -= OnJumpIn;
            cameraReelController.Activated -= TrackCameraReelOpen;
        }

        private void OnJumpIn(Vector2Int parcel)
        {
            analytics.Track(AnalyticsEvents.Map.JUMP_IN, new JsonObject
            {
                { "parcel", parcel.ToString() },
            });
        }

        private void TrackCameraReelOpen() =>
            analytics.Track(AnalyticsEvents.CameraReel.CAMERA_REEL_OPEN);
    }
}
