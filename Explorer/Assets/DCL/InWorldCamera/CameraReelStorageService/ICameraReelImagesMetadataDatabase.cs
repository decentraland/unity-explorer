﻿using Cysharp.Threading.Tasks;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using System.Threading;

namespace DCL.InWorldCamera.CameraReelStorageService
{
    internal interface ICameraReelImagesMetadataDatabase
    {
        UniTask<CameraReelStorageResponse> GetStorageInfoAsync(string userAddress, CancellationToken ct);

        UniTask<CameraReelResponses> GetScreenshotsAsync(string userAddress, int limit, int offset, CancellationToken ct);

        UniTask<CameraReelUploadResponse> UploadScreenshotAsync(byte[] image, ScreenshotMetadata metadata, CancellationToken ct);

        UniTask<CameraReelStorageResponse> DeleteScreenshotAsync(string uuid, CancellationToken ct);
    }
}