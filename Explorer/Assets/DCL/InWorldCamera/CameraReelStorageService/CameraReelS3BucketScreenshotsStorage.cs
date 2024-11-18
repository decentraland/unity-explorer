using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using System.Threading;
using UnityEngine;

namespace DCL.InWorldCamera.CameraReelStorageService
{
    public class CameraReelS3BucketScreenshotsStorage : ICameraReelScreenshotsStorage
    {
        private readonly IWebRequestController webRequestController;

        public CameraReelS3BucketScreenshotsStorage(IWebRequestController webRequestController)
        {
            this.webRequestController = webRequestController;
        }

        public async UniTask<Texture2D> GetScreenshotImageAsync(string url, CancellationToken ct = default) =>
            await GetImageAsync(url, ct);

        public async UniTask<Texture2D> GetScreenshotThumbnailAsync(string url, CancellationToken ct = default) =>
            await GetImageAsync(url, ct);

        private async UniTask<Texture2D> GetImageAsync(string url, CancellationToken ct = default) =>
            await webRequestController.GetTextureAsync(
                new CommonArguments(URLAddress.FromString(url)),
                new GetTextureArguments(false),
                GetTextureWebRequest.CreateTexture(TextureWrapMode.Clamp), ct, ReportCategory.CAMERA_REEL);
    }
}
