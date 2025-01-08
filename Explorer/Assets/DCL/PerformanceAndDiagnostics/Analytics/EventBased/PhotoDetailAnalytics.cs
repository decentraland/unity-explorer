using DCL.InWorldCamera.PhotoDetail;
using System;

namespace DCL.PerformanceAndDiagnostics.Analytics.EventBased
{
    public class PhotoDetailAnalytics : IDisposable
    {
        private readonly IAnalyticsController analytics;
        private readonly PhotoDetailController photoDetailController;
        private readonly PhotoDetailInfoController photoDetailInfoController;

        public PhotoDetailAnalytics(IAnalyticsController analytics, PhotoDetailController controller)
        {
            this.analytics = analytics;
            this.photoDetailController = controller;
            this.photoDetailInfoController = controller.PhotoDetailInfoController;

            photoDetailController.Activated += TrackPhotoDetailOpen;
            photoDetailController.JumpToPhotoPlace += TrackPhotoDetailJumpToPlace;
            photoDetailInfoController.WearableMarketClicked += TrackWearableMarketClicked;
            photoDetailController.ScreenshotShared += TrackScreenshotShared;
            photoDetailController.ScreenshotDownloaded += TrackScreenshotDownloaded;
        }

        public void Dispose()
        {
            photoDetailController.Activated -= TrackPhotoDetailOpen;
            photoDetailController.JumpToPhotoPlace -= TrackPhotoDetailJumpToPlace;
            photoDetailInfoController.WearableMarketClicked -= TrackWearableMarketClicked;
            photoDetailController.ScreenshotShared -= TrackScreenshotShared;
            photoDetailController.ScreenshotDownloaded -= TrackScreenshotDownloaded;
        }

        private void TrackScreenshotDownloaded() =>
            analytics.Track(AnalyticsEvents.CameraReel.DOWNLOAD_PHOTO);

        private void TrackScreenshotShared() =>
            analytics.Track(AnalyticsEvents.CameraReel.SHARE_PHOTO);

        private void TrackWearableMarketClicked() =>
            analytics.Track(AnalyticsEvents.CameraReel.PHOTO_TO_MARKETPLACE);

        private void TrackPhotoDetailJumpToPlace() =>
            analytics.Track(AnalyticsEvents.CameraReel.PHOTO_JUMP_TO);

        private void TrackPhotoDetailOpen() =>
            analytics.Track(AnalyticsEvents.CameraReel.OPEN_PHOTO);
    }
}
