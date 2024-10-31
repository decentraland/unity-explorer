using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using System.Threading;
using UnityEngine;

namespace DCL.InWorldCamera.CameraReelStorageService
{
    public class CameraReelS3BucketImagesStorage : ICameraReelImagesStorage
    {
        private readonly IWebRequestController webRequestController;

        public CameraReelS3BucketImagesStorage(IWebRequestController webRequestController)
        {
            this.webRequestController = webRequestController;
        }

        public async UniTask<Texture2D> GetScreenshotImage(string url) =>
            await GetImage(url);

        public async UniTask<Texture2D> GetScreenshotThumbnail(string url) =>
            await GetImage(url);

        private async UniTask<Texture2D> GetImage(string url) =>
            await webRequestController.GetTextureAsync(
                new CommonArguments(URLAddress.FromString(url)),
                new GetTextureArguments(false),
                GetTextureWebRequest.CreateTexture(TextureWrapMode.Clamp), default(CancellationToken), ReportCategory.CAMERA_REEL);
    }
}
