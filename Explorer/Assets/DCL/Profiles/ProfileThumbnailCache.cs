using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Profiles
{
    public class ProfileThumbnailCache : IProfileThumbnailCache
    {
        private const int PIXELS_PER_UNIT = 50;

        private readonly IWebRequestController webRequestController;
        private readonly Dictionary<string, Sprite> thumbnails = new ();

        public ProfileThumbnailCache(IWebRequestController webRequestController)
        {
            this.webRequestController = webRequestController;
        }

        public async UniTask<Sprite?> GetThumbnailAsync(string userId, string thumbnailUrl, CancellationToken ct)
        {
            Sprite? sprite = GetThumbnailFromCache(userId);
            if (sprite != null)
                return sprite;

            return await DownloadThumbnailAsync(userId, thumbnailUrl, ct);
        }

        private async UniTask<Sprite?> DownloadThumbnailAsync(string userId, string thumbnailUrl, CancellationToken ct)
        {
            if (URLAddress.EMPTY.Equals(thumbnailUrl)) return null;

            try
            {
                IOwnedTexture2D ownedTexture = await webRequestController.GetTextureAsync(
                    new CommonArguments(URLAddress.FromString(thumbnailUrl)),
                    new GetTextureArguments(TextureType.Albedo),
                    GetTextureWebRequest.CreateTexture(TextureWrapMode.Clamp),
                    ct,
                    ReportCategory.UI
                );

                var texture = ownedTexture.Texture;
                texture.filterMode = FilterMode.Bilinear;
                Sprite downloadedSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), VectorUtilities.OneHalf, PIXELS_PER_UNIT, 0, SpriteMeshType.FullRect, Vector4.one, false);
                SetThumbnailIntoCache(userId, downloadedSprite);

                return downloadedSprite;
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.FRIENDS));
                return null;
            }
        }

        private Sprite? GetThumbnailFromCache(string userId) =>
            thumbnails.GetValueOrDefault(userId);

        private void SetThumbnailIntoCache(string userId, Sprite sprite) =>
            thumbnails[userId] = sprite;
    }
}
