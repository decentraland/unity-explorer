using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;

namespace DCL.InWorldCamera.CameraReelStorageService
{
    /// <summary>
    ///     Camera reel remote storage: the images metadata database and the S3 screenshots bucket.
    /// </summary>
    public class CameraReelContainer
    {
        public CameraReelRemoteStorageService StorageService { get; }

        public ICameraReelScreenshotsStorage ScreenshotsStorage { get; }

        public GalleryEventBus GalleryEventBus { get; }

        private CameraReelContainer(CameraReelRemoteStorageService storageService, ICameraReelScreenshotsStorage screenshotsStorage, GalleryEventBus galleryEventBus)
        {
            StorageService = storageService;
            ScreenshotsStorage = screenshotsStorage;
            GalleryEventBus = galleryEventBus;
        }

        public static CameraReelContainer Create(IWebRequestController webRequestController, IDecentralandUrlsSource urlsSource, string? userAddress)
        {
            ICameraReelImagesMetadataDatabase imagesMetadataDatabase = new CameraReelImagesMetadataRemoteDatabase(webRequestController, urlsSource);
            ICameraReelScreenshotsStorage screenshotsStorage = new CameraReelS3BucketScreenshotsStorage(webRequestController);

            return new CameraReelContainer(
                new CameraReelRemoteStorageService(imagesMetadataDatabase, screenshotsStorage, userAddress),
                screenshotsStorage,
                new GalleryEventBus());
        }
    }
}
