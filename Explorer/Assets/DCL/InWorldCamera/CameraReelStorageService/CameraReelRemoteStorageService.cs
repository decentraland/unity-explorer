using Cysharp.Threading.Tasks;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.InWorldCamera.CameraReelStorageService
{
    public class CameraReelRemoteStorageService : ICameraReelStorageService, ICameraReelScreenshotsStorage
    {
        private readonly ICameraReelImagesMetadataDatabase imagesMetadataDatabase;
        private readonly ICameraReelScreenshotsStorage screenshotsStorage;

        public event Action<CameraReelResponse, CameraReelStorageStatus> ScreenshotUploaded;

        public CameraReelRemoteStorageService(ICameraReelImagesMetadataDatabase imagesMetadataDatabase, ICameraReelScreenshotsStorage screenshotsStorage)
        {
            this.imagesMetadataDatabase = imagesMetadataDatabase;
            this.screenshotsStorage = screenshotsStorage;
        }

        public async UniTask<CameraReelStorageStatus> GetUserGalleryStorageInfoAsync(string userAddress, CancellationToken ct = default)
        {
            CameraReelStorageResponse response = await imagesMetadataDatabase.GetStorageInfoAsync(userAddress, ct);
            return new CameraReelStorageStatus(response.currentImages, response.maxImages);
        }

        public async UniTask<CameraReelResponses> GetScreenshotGalleryAsync(string userAddress, int limit, int offset, CancellationToken ct) =>
            await imagesMetadataDatabase.GetScreenshotsAsync(userAddress, limit, offset, ct);

        public async UniTask<CameraReelStorageStatus> DeleteScreenshotAsync(string uuid, CancellationToken ct = default)
        {
            CameraReelStorageResponse response = await imagesMetadataDatabase.DeleteScreenshotAsync(uuid, ct);
            return new CameraReelStorageStatus(response.currentImages, response.maxImages);
        }

        public async UniTask<CameraReelStorageStatus> UploadScreenshotAsync(Texture2D image, ScreenshotMetadata metadata, CancellationToken ct = default)
        {
            CameraReelUploadResponse response = await imagesMetadataDatabase.UploadScreenshotAsync(image.EncodeToJPG(), metadata, ct);

            var storageStatus = new CameraReelStorageStatus(response.currentImages, response.maxImages);
            ScreenshotUploaded?.Invoke(response.image, storageStatus);
            return storageStatus;
        }

        public UniTask<Texture2D> GetScreenshotImageAsync(string url) =>
            screenshotsStorage.GetScreenshotImageAsync(url);

        public UniTask<Texture2D> GetScreenshotThumbnailAsync(string url) =>
            screenshotsStorage.GetScreenshotThumbnailAsync(url);
    }
}
