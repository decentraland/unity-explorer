using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Profiles;
using DCL.WebRequests;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Friends
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

        public Sprite? GetThumbnail(string userId) =>
            thumbnails.GetValueOrDefault(userId);

        public async UniTask<Sprite?> GetThumbnailAsync(Profile profile, CancellationToken ct)
        {
            Sprite? sprite = GetThumbnail(profile.UserId);
            if (sprite != null)
                return sprite;

            if (profile.Avatar.FaceSnapshotUrl.Equals(URLAddress.EMPTY)) return null;

            IOwnedTexture2D ownedTexture = await webRequestController.GetTextureAsync(
                new CommonArguments(URLAddress.FromString(profile.Avatar.FaceSnapshotUrl)),
                new GetTextureArguments(TextureType.Albedo),
                GetTextureWebRequest.CreateTexture(TextureWrapMode.Clamp),
                ct,
                ReportCategory.UI
            );

            var texture = ownedTexture.Texture;
            texture.filterMode = FilterMode.Bilinear;
            Sprite downloadedSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), VectorUtilities.OneHalf, PIXELS_PER_UNIT, 0, SpriteMeshType.FullRect, Vector4.one, false);

            SetThumbnail(profile.UserId, downloadedSprite);

            return downloadedSprite;
        }

        public void SetThumbnail(string userId, Sprite sprite) =>
            thumbnails[userId] = sprite;

    }
}
