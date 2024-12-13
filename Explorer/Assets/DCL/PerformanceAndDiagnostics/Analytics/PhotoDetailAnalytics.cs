using DCL.InWorldCamera.PhotoDetail;
using System;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class PhotoDetailAnalytics : IDisposable
    {
        private readonly IAnalyticsController analytics;
        private readonly PhotoDetailController photoDetailController;

        public PhotoDetailAnalytics(IAnalyticsController analytics, PhotoDetailController controller)
        {
            this.analytics = analytics;
            this.photoDetailController = controller;

            photoDetailController.Activated += TrackPhotoDetailOpen;
            photoDetailController.JumpToPhotoPlace += TrackPhotoDetailJumpToPlace;
        }

        public void Dispose()
        {
            photoDetailController.Activated -= TrackPhotoDetailOpen;
            photoDetailController.JumpToPhotoPlace -= TrackPhotoDetailJumpToPlace;
        }

        private void TrackPhotoDetailJumpToPlace() =>
            analytics.Track(AnalyticsEvents.CameraReel.PHOTO_JUMP_TO);

        private void TrackPhotoDetailOpen() =>
            analytics.Track(AnalyticsEvents.CameraReel.OPEN_PHOTO);
    }
}
