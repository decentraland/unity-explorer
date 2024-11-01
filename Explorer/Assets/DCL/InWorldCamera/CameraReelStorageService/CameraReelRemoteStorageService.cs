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

        internal CameraReelRemoteStorageService(ICameraReelImagesMetadataDatabase imagesMetadataDatabase)
        {
            this.imagesMetadataDatabase = imagesMetadataDatabase;
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
    }
}
