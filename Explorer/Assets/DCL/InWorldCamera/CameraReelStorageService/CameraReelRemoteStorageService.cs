using Cysharp.Threading.Tasks;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.InWorldCamera.CameraReelStorageService
{
    public class CameraReelRemoteStorageService : ICameraReelStorageService
    {
        private readonly ICameraReelImagesMetadataDatabase imagesMetadataDatabase;

        public event Action<CameraReelResponse, CameraReelStorageStatus> ScreenshotUploaded;

        public CameraReelRemoteStorageService(ICameraReelImagesMetadataDatabase imagesMetadataDatabase)
        {
            this.imagesMetadataDatabase = imagesMetadataDatabase;
        }

        public async UniTask<CameraReelStorageStatus> GetUserGalleryStorageInfo(string userAddress, CancellationToken ct = default)
        {
            CameraReelStorageResponse response = await imagesMetadataDatabase.GetStorageInfo(userAddress, ct);
            return new CameraReelStorageStatus(response.currentImages, response.maxImages);
        }

        public async UniTask<CameraReelResponses> GetScreenshotGallery(string userAddress, int limit, int offset, CancellationToken ct) =>
            await imagesMetadataDatabase.GetScreenshots(userAddress, limit, offset, ct);

        public async UniTask<CameraReelStorageStatus> DeleteScreenshot(string uuid, CancellationToken ct = default)
        {
            CameraReelStorageResponse response = await imagesMetadataDatabase.DeleteScreenshot(uuid, ct);
            return new CameraReelStorageStatus(response.currentImages, response.maxImages);
        }

        public async UniTask<CameraReelStorageStatus> UploadScreenshot(Texture2D image, ScreenshotMetadata metadata, CancellationToken ct = default)
        {
            CameraReelUploadResponse response = await imagesMetadataDatabase.UploadScreenshot(image.EncodeToJPG(), metadata, ct);

            var storageStatus = new CameraReelStorageStatus(response.currentImages, response.maxImages);
            ScreenshotUploaded?.Invoke(response.image, storageStatus);
            return storageStatus;
        }
    }
}
