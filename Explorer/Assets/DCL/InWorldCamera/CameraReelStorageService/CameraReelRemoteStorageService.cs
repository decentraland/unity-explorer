﻿using Cysharp.Threading.Tasks;
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
        public CameraReelStorageStatus StorageStatus { get; private set; }

        public event Action<CameraReelResponse, CameraReelStorageStatus, string>? ScreenshotUploaded;

        public CameraReelRemoteStorageService(ICameraReelImagesMetadataDatabase imagesMetadataDatabase, ICameraReelScreenshotsStorage screenshotsStorage, string userAddress)
        {
            this.imagesMetadataDatabase = imagesMetadataDatabase;
            this.screenshotsStorage = screenshotsStorage;

            if (!string.IsNullOrEmpty(userAddress))
                GetUserGalleryStorageInfoAsync(userAddress).Forget();
        }

        public async UniTask<CameraReelStorageStatus> GetUserGalleryStorageInfoAsync(string userAddress, CancellationToken ct = default)
        {
            CameraReelStorageResponse response = await imagesMetadataDatabase.GetStorageInfoAsync(userAddress, ct);

            StorageStatus = new CameraReelStorageStatus(response.currentImages, response.maxImages);
            return StorageStatus;
        }

        public async UniTask<CameraReelResponses> GetScreenshotGalleryAsync(string userAddress, int limit, int offset, CancellationToken ct) =>
            await imagesMetadataDatabase.GetScreenshotsAsync(userAddress, limit, offset, ct);

        public async UniTask<CameraReelResponsesCompact> GetCompactScreenshotGalleryAsync(string userAddress, int limit, int offset, CancellationToken ct) =>
            await imagesMetadataDatabase.GetCompactScreenshotsAsync(userAddress, limit, offset, ct);

        public async UniTask<CameraReelStorageStatus> DeleteScreenshotAsync(string uuid, CancellationToken ct = default)
        {
            CameraReelStorageResponse response = await imagesMetadataDatabase.DeleteScreenshotAsync(uuid, ct);

            StorageStatus = new CameraReelStorageStatus(response.currentImages, response.maxImages);
            return StorageStatus;
        }

        public async UniTask UpdateScreenshotVisibilityAsync(string uuid, bool isPublic, CancellationToken ct = default) =>
            await imagesMetadataDatabase.UpdateScreenshotVisibilityAsync(uuid, isPublic, ct);

        public async UniTask<CameraReelStorageStatus> UploadScreenshotAsync(Texture2D image, ScreenshotMetadata metadata, string source, CancellationToken ct = default)
        {
            if (!StorageStatus.HasFreeSpace) return StorageStatus;

            CameraReelUploadResponse response = await imagesMetadataDatabase.UploadScreenshotAsync(image.EncodeToJPG(), metadata, ct);

            StorageStatus = new CameraReelStorageStatus(response.currentImages, response.maxImages);
            ScreenshotUploaded?.Invoke(response.image, StorageStatus, source);
            return StorageStatus;
        }

        public UniTask<Texture2D> GetScreenshotImageAsync(string url, CancellationToken ct = default) =>
            screenshotsStorage.GetScreenshotImageAsync(url, ct);

        public UniTask<Texture2D> GetScreenshotThumbnailAsync(string url, CancellationToken ct = default) =>
            screenshotsStorage.GetScreenshotThumbnailAsync(url, ct);
    }
}
