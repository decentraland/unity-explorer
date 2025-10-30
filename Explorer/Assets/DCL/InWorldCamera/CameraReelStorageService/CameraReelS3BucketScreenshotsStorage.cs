using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

namespace DCL.InWorldCamera.CameraReelStorageService
{
    public class CameraReelS3BucketScreenshotsStorage : ICameraReelScreenshotsStorage
    {
        private readonly IWebRequestController webRequestController;

        public CameraReelS3BucketScreenshotsStorage(IWebRequestController webRequestController)
        {
            this.webRequestController = webRequestController;
        }

        public async UniTask<Texture2D> GetScreenshotImageAsync(string url, bool compressed = false, CancellationToken ct = default) =>
            compressed ? await GetImageAsync(url, ct) : await GetUncompressedImageAsync(url, ct);

        public async UniTask<Texture2D> GetScreenshotThumbnailAsync(string url, CancellationToken ct = default) =>
            await GetImageAsync(url, ct);

        // TODO memory disposing
        private async UniTask<Texture2D> GetImageAsync(string url, CancellationToken ct = default)
        {
            var texture = await webRequestController.GetTextureAsync(
                new CommonArguments(URLAddress.FromString(url)),
                new GetTextureArguments(TextureType.Albedo),
                GetTextureWebRequest.CreateTexture(TextureWrapMode.Clamp), ct, ReportCategory.CAMERA_REEL);

            return texture.Texture;
        }

        private async UniTask<Texture2D> GetUncompressedImageAsync(string reelUrl, CancellationToken ct)
        {
            using (UnityWebRequest webRequest = UnityWebRequestTexture.GetTexture(reelUrl))
            {
                await webRequest.SendWebRequest().ToUniTask(cancellationToken: ct);

                if (webRequest.result != UnityWebRequest.Result.Success)
                    throw new Exception($"Error while downloading reel: {webRequest.error}");

                return DownloadHandlerTexture.GetContent(webRequest);
            }
        }
    }
}
