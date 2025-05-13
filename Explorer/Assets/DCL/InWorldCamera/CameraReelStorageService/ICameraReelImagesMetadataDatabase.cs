using Cysharp.Threading.Tasks;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using System.Threading;

namespace DCL.InWorldCamera.CameraReelStorageService
{
    public interface ICameraReelImagesMetadataDatabase
    {
        UniTask<CameraReelStorageResponse> GetStorageInfoAsync(string userAddress, CancellationToken ct);
        UniTask<CameraReelStorageResponse> UnsignedGetStorageInfoAsync(string userAddress, CancellationToken ct);

        UniTask<CameraReelResponses> GetScreenshotsAsync(string userAddress, int limit, int offset, CancellationToken ct);
        UniTask<CameraReelResponse> GetScreenshotsMetadataAsync(string uuid, CancellationToken ct);
        UniTask<CameraReelResponsesCompact> GetCompactScreenshotsAsync(string userAddress, int limit, int offset, CancellationToken ct);
        UniTask<CameraReelResponsesCompact> UnsignedGetCompactScreenshotsAsync(string userAddress, int limit, int offset, CancellationToken ct);
        UniTask<CameraReelResponsesCompact> GetCompactPlaceScreenshotsAsync(string placeId, int limit, int offset, CancellationToken ct);
        UniTask<CameraReelResponsesCompact> GetCompactCommunityScreenshotsAsync(string communityId, int limit, int offset, CancellationToken ct);

        UniTask<CameraReelUploadResponse> UploadScreenshotAsync(byte[] image, ScreenshotMetadata metadata, CancellationToken ct);

        UniTask<CameraReelStorageResponse> DeleteScreenshotAsync(string uuid, CancellationToken ct);

        UniTask UpdateScreenshotVisibilityAsync(string uuid, bool isPublic, CancellationToken ct);
    }
}
