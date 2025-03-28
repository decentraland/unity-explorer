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
        private struct RequestAttempts
        {
            private const int ATTEMPT_SECONDS_UNIT = 30;
            private const int MAX_ATTEMPTS = 5;

            private int attempts;
            private DateTime nextAvailableAttempt;

            private RequestAttempts(int attempts, DateTime nextAttempt)
            {
                this.attempts = attempts;
                this.nextAvailableAttempt = nextAttempt;
            }

            public bool CanIncreaseCooldown() => attempts < MAX_ATTEMPTS;

            public void IncreaseCooldown()
            {
                nextAvailableAttempt = DateTime.Now.AddSeconds(ATTEMPT_SECONDS_UNIT * attempts);
                attempts++;
            }

            public bool CanRetry() =>
                DateTime.Now >= nextAvailableAttempt;

            public static RequestAttempts FirstAttempt() =>
                new (1, DateTime.Now.AddSeconds(ATTEMPT_SECONDS_UNIT));
        }

        private const int PIXELS_PER_UNIT = 50;

        private readonly IWebRequestController webRequestController;
        private readonly Dictionary<string, Sprite> thumbnails = new ();
        private readonly Dictionary<string, RequestAttempts> failedThumbnails = new ();
        private readonly HashSet<string> unsolvableThumbnails = new ();

        public ProfileThumbnailCache(IWebRequestController webRequestController)
        {
            this.webRequestController = webRequestController;
        }

        public Sprite? GetThumbnail(string userId) =>
            thumbnails.GetValueOrDefault(userId);

        public async UniTask<Sprite?> GetThumbnailAsync(string userId, string thumbnailUrl, CancellationToken ct)
        {
            Sprite? sprite = GetThumbnail(userId);
            if (sprite != null)
                return sprite;

            return await DownloadThumbnailAsync(userId, thumbnailUrl, ct);
        }

        private async UniTask<Sprite?> DownloadThumbnailAsync(string userId, string thumbnailUrl, CancellationToken ct)
        {
            if (URLAddress.EMPTY.Equals(thumbnailUrl)) return null;

            if (unsolvableThumbnails.Contains(userId) || !TestCooldownCondition(userId)) return null;

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
                Sprite downloadedSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                    VectorUtilities.OneHalf, PIXELS_PER_UNIT, 0, SpriteMeshType.FullRect, Vector4.one, false);
                SetThumbnailIntoCache(userId, downloadedSprite);
                failedThumbnails.Remove(userId);

                return downloadedSprite;
            }
            catch (OperationCanceledException e)
            {
                return null;
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.PROFILE));

                if (failedThumbnails.TryGetValue(userId, out RequestAttempts requestAttempts))
                    if (requestAttempts.CanIncreaseCooldown())
                        requestAttempts.IncreaseCooldown();
                    else
                    {
                        unsolvableThumbnails.Add(userId);
                        failedThumbnails.Remove(userId);
                    }
                else
                    failedThumbnails[userId] = RequestAttempts.FirstAttempt();

                return null;
            }
        }

        private bool TestCooldownCondition(string userId) =>
            !failedThumbnails.TryGetValue(userId, out RequestAttempts requestAttempts) || requestAttempts.CanRetry();

        private void SetThumbnailIntoCache(string userId, Sprite sprite) =>
            thumbnails[userId] = sprite;
    }
}
