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

        public CameraReelStorageResponse Storage;

        [Header("GALLERY")]
        public int Limit = 10;
        public int Offset;

        [Space(5)]
        public CameraReelResponses Result;
        public Texture2D ImageTexture;
        public Texture2D ThumbnailTexture;

        [Header("UPLOAD")]
        public string ThumbnailUrl;

        private ICameraReelImagesStorage imagesStorageInternal;
        private ICameraReelImagesStorage imagesStorage => imagesStorageInternal ??= new CameraReelS3BucketImagesStorage(webRequestController);

        private ICameraReelImagesMetadataDatabase metadataDatabase
        {
            get
            {
                var urlsSource = new DecentralandUrlsSource(Env);
                return new CameraReelImagesMetadataRemoteDatabase(webRequestController, urlsSource);
            }
        }

        [ContextMenu(nameof(GET_STORAGE))]
        public async void GET_STORAGE()
        {
            Storage = await metadataDatabase.GetStorageInfo(identity.Identity.Address, ct);
        }

        [ContextMenu(nameof(GET_GALLERY))]
        public async void GET_GALLERY()
        {
            Result = await metadataDatabase.GetScreenshots(identity.Identity.Address, Limit, Offset, ct);

            Storage.currentImages = Result.currentImages;
            Storage.maxImages = Result.maxImages;

            CameraReelResponse screenshot = Result.images.First();
            ImageTexture = await imagesStorage.GetScreenshotImage(screenshot.url);
            ThumbnailTexture = await imagesStorage.GetScreenshotThumbnail(screenshot.thumbnailUrl);
        }

        [ContextMenu(nameof(UPLOAD_IMAGE))]
        public async void UPLOAD_IMAGE()
        {
            CameraReelUploadResponse response = await metadataDatabase.UploadScreenshot(ImageTexture.EncodeToJPG(), Result.images.First().metadata, ct);

            Storage.currentImages = response.currentImages;
            Storage.maxImages = response.maxImages;

            ThumbnailUrl = response.image.thumbnailUrl;
        }

        [ContextMenu(nameof(DELETE_IMAGE))]
        public async void DELETE_IMAGE()
        {
            Storage = await metadataDatabase.DeleteScreenshot(Result.images.First().id, ct);
        }
    }
}
