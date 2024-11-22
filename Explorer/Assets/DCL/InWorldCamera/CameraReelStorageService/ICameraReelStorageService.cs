using Cysharp.Threading.Tasks;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.InWorldCamera.CameraReelStorageService
{
    public interface ICameraReelStorageService
    {
        event Action<CameraReelResponse, CameraReelStorageStatus>? ScreenshotUploaded;

        UniTask<CameraReelStorageStatus> GetUserGalleryStorageInfoAsync(string userAddress, CancellationToken ct = default);

        UniTask<CameraReelResponses> GetScreenshotGalleryAsync(string userAddress, int limit, int offset, CancellationToken ct = default);
        UniTask<CameraReelResponsesCompact> GetCompactScreenshotGalleryAsync(string userAddress, int limit, int offset, CancellationToken ct = default);

        UniTask<CameraReelStorageStatus> DeleteScreenshotAsync(string uuid, CancellationToken ct = default);

        UniTask<CameraReelStorageStatus> UploadScreenshotAsync(Texture2D image, ScreenshotMetadata metadata, CancellationToken ct = default);
        UniTask UpdateScreenshotVisibilityAsync(string uuid, bool isPublic, CancellationToken ct = default);

        UniTask<CameraReelResponse> GetScreenshotsMetadataAsync(string uuid, CancellationToken ct = default);
    }
}
