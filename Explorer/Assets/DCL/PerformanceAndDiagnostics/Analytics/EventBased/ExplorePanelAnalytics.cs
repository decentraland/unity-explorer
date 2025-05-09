using DCL.ExplorePanel;
using DCL.InWorldCamera.CameraReelGallery;
using DCL.Navmap;
using DCL.Settings;
using DCL.Settings.Settings;
using Segment.Serialization;
using System;
using UnityEngine;

namespace DCL.PerformanceAndDiagnostics.Analytics.EventBased
{
    public class ExplorePanelAnalytics : IDisposable
    {
        private readonly IAnalyticsController analytics;
        private readonly NavmapController navmapController;
        private readonly CameraReelController cameraReelController;
        private readonly CameraReelGalleryController cameraReelGalleryController;
        private readonly SettingsController settingsController;

        public ExplorePanelAnalytics(IAnalyticsController analytics, ExplorePanelController controller)
        {
            this.analytics = analytics;
            this.navmapController = controller.NavmapController;
            this.cameraReelController = controller.CameraReelController;
            this.cameraReelGalleryController = this.cameraReelController.CameraReelGalleryController;
            this.settingsController = controller.SettingsController;

            cameraReelController.Activated += TrackCameraReelOpen;
            cameraReelGalleryController.ScreenshotDeleted += TrackScreenshotDeleted;
            cameraReelGalleryController.ScreenshotDownloaded += TrackScreenshotDownloaded;
            cameraReelGalleryController.ScreenshotShared += TrackScreenshotShared;
            settingsController.ChatBubblesVisibilityChanged += OnChatBubblesVisibilityChanged;
        }

        public void Dispose()
        {
            cameraReelController.Activated -= TrackCameraReelOpen;
            cameraReelGalleryController.ScreenshotDeleted -= TrackScreenshotDeleted;
            cameraReelGalleryController.ScreenshotDownloaded -= TrackScreenshotDownloaded;
            cameraReelGalleryController.ScreenshotShared -= TrackScreenshotShared;
            settingsController.ChatBubblesVisibilityChanged -= OnChatBubblesVisibilityChanged;
        }

        private void TrackScreenshotDownloaded() =>
            analytics.Track(AnalyticsEvents.CameraReel.DOWNLOAD_PHOTO);

        private void TrackScreenshotShared() =>
            analytics.Track(AnalyticsEvents.CameraReel.SHARE_PHOTO);

        private void OnJumpIn(Vector2Int parcel)
        {
            analytics.Track(AnalyticsEvents.Map.JUMP_IN, new JsonObject
            {
                { "parcel", parcel.ToString() },
            });
        }

        private void TrackCameraReelOpen() =>
            analytics.Track(AnalyticsEvents.CameraReel.CAMERA_REEL_OPEN);

        private void TrackScreenshotDeleted() =>
            analytics.Track(AnalyticsEvents.CameraReel.DELETE_PHOTO);

        private void OnChatBubblesVisibilityChanged(ChatBubbleVisibilitySettings visibility)
        {
            analytics.Track(AnalyticsEvents.Settings.CHAT_BUBBLES_VISIBILITY_CHANGED, new JsonObject
            {
                { "visibility",  visibility == ChatBubbleVisibilitySettings.NONE ? "none" : visibility == ChatBubbleVisibilitySettings.NEARBY_ONLY ? "nearby" : "all"},
            });
        }
    }
}
