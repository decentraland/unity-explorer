using DCL.Browser.DecentralandUrls;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Web3.Identities;
using DCL.WebRequests;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace DCL.InWorldCamera.CameraReelStorageService.Playground
{
    public class CameraReelRemoteServicesManualTest : MonoBehaviour
    {
        private readonly IWeb3IdentityCache.Default identity = new ();
        private readonly IWebRequestController webRequestController = IWebRequestController.DEFAULT;
        private readonly CancellationToken ct = CancellationToken.None;

        public DecentralandEnvironment Env;

        [Space(5)]
        public CameraReelStorageResponse Storage;

        [Header("GALLERY")]
        public int Limit = 10;
        public int Offset;

        [Space(5)]
        public CameraReelResponses Result;
        public Texture2D ImageTexture;
        public Texture2D ThumbnailTexture;

        [Header("UPLOAD")]
        public ScreenshotMetadata Metadata;
        public Texture2D Screenshot;
        [Space(5)]
        public string ResultThumbnailUrl;

        private ICameraReelScreenshotsStorage screenshotsStorageInternal;
        private ICameraReelScreenshotsStorage screenshotsStorage => screenshotsStorageInternal ??= new CameraReelS3BucketScreenshotsStorage(webRequestController);

        private ICameraReelImagesMetadataDatabase metadataDatabase
        {
            get
            {
                var urlsSource = new DecentralandUrlsSource(Env);
                return new CameraReelImagesMetadataRemoteDatabase(webRequestController, urlsSource);
            }
        }

        [ContextMenu("GET STORAGE")]
        public async void GetStorageAsync()
        {
            Storage = await metadataDatabase.GetStorageInfoAsync(identity.Identity.Address, ct);
        }

        [ContextMenu("GET GALLERY")]
        public async void GetGalleryAsync()
        {
            Result = await metadataDatabase.GetScreenshotsAsync(identity.Identity.Address, Limit, Offset, ct);

            Storage.currentImages = Result.currentImages;
            Storage.maxImages = Result.maxImages;

            CameraReelResponse screenshot = Result.images.First();
            ImageTexture = await screenshotsStorage.GetScreenshotImageAsync(screenshot.url);
            ThumbnailTexture = await screenshotsStorage.GetScreenshotThumbnailAsync(screenshot.thumbnailUrl);
        }

        [ContextMenu("UPLOAD IMAGE")]
        public async void UploadImageAsync()
        {
            CameraReelUploadResponse response = await metadataDatabase.UploadScreenshotAsync(Screenshot.EncodeToJPG(),Metadata, ct);

            Storage.currentImages = response.currentImages;
            Storage.maxImages = response.maxImages;

            ResultThumbnailUrl = response.image.thumbnailUrl;
        }


        [ContextMenu("DELETE IMAGE")]
        public async void DeleteImageAsync()
        {
            Storage = await metadataDatabase.DeleteScreenshotAsync(Result.images.First().id, ct);
        }
    }
}
