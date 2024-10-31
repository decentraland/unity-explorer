using Cysharp.Threading.Tasks;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using System.Threading;

namespace DCL.InWorldCamera.CameraReelStorageService
{
    public interface ICameraReelImagesMetadataDatabase
    {
        UniTask<CameraReelStorageResponse> GetStorageInfo(string userAddress, CancellationToken ct);

        UniTask<CameraReelResponses> GetScreenshots(string userAddress, int limit, int offset, CancellationToken ct);

        UniTask<CameraReelUploadResponse> UploadScreenshot(byte[] image, ScreenshotMetadata metadata, CancellationToken ct);

        UniTask<CameraReelStorageResponse> DeleteScreenshot(string uuid, CancellationToken ct);
    }
}
