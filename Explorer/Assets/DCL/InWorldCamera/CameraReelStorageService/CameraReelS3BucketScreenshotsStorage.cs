using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using DCL.WebRequests.ArgsFactory;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using System.Threading;
using UnityEngine;

namespace DCL.InWorldCamera.CameraReelStorageService
{
    public class CameraReelS3BucketScreenshotsStorage : ICameraReelScreenshotsStorage
    {
        private readonly IWebRequestController webRequestController;
        private readonly IGetTextureArgsFactory getTextureArgsFactory;

        public CameraReelS3BucketScreenshotsStorage(IWebRequestController webRequestController, IGetTextureArgsFactory getTextureArgsFactory)
        {
            this.webRequestController = webRequestController;
            this.getTextureArgsFactory = getTextureArgsFactory;
        }

        public async UniTask<Texture2D> GetScreenshotImageAsync(string url) =>
            await GetImageAsync(url);

        public async UniTask<Texture2D> GetScreenshotThumbnailAsync(string url) =>
            await GetImageAsync(url);

        // TODO memory disposing
        private async UniTask<Texture2D> GetImageAsync(string url)
        {
            var texture = await webRequestController.GetTextureAsync(
                new CommonArguments(URLAddress.FromString(url)),
                getTextureArgsFactory.NewArguments(TextureType.Albedo),
                GetTextureWebRequest.CreateTexture(TextureWrapMode.Clamp), default(CancellationToken), ReportCategory.CAMERA_REEL);

            return texture.Texture;
        }
    }
}
