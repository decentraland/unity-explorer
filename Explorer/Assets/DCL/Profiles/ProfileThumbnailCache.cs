using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
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
            public const int MAX_ATTEMPTS = 5;
            private const int ATTEMPT_SECONDS_UNIT = 30;

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
                attempts++;
                nextAvailableAttempt = DateTime.Now.AddSeconds(ATTEMPT_SECONDS_UNIT * attempts);
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
        private readonly Dictionary<string, UniTaskCompletionSource<Sprite?>> currentThumbnailTasks = new ();
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

            //Avoid multiple requests for the same thumbnail
            if (currentThumbnailTasks.TryGetValue(userId, out UniTaskCompletionSource<Sprite?> thumbnailTask))
                return await thumbnailTask.Task;

            UniTaskCompletionSource<Sprite?> spriteTaskCompletionSource = new UniTaskCompletionSource<Sprite?>();
            if (currentThumbnailTasks.TryAdd(userId, spriteTaskCompletionSource))
                DownloadThumbnailAsync(userId, thumbnailUrl, spriteTaskCompletionSource, ct).Forget();

            return await spriteTaskCompletionSource.Task;
        }

        private async UniTaskVoid DownloadThumbnailAsync(string userId, string thumbnailUrl, UniTaskCompletionSource<Sprite?> tcs, CancellationToken ct)
        {
            Sprite? result = null;

            if (URLAddress.EMPTY.Equals(thumbnailUrl))
            {
                FinalizeTask(tcs, result, userId);
                return;
            }

            if (unsolvableThumbnails.Contains(userId) || !TestCooldownCondition(userId))
            {
                FinalizeTask(tcs, result, userId);
                return;
            }

            try
            {
                IOwnedTexture2D ownedTexture = await webRequestController.GetTextureAsync(
                    new CommonArguments(URLAddress.FromString(thumbnailUrl)),
                    new GetTextureArguments(TextureType.Albedo),
                    GetTextureWebRequest.CreateTexture(TextureWrapMode.Clamp),
                    ct,
                    ReportCategory.UI,
                    suppressErrors: true
                );

                var texture = ownedTexture.Texture;
                texture.filterMode = FilterMode.Bilinear;

                result = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                    VectorUtilities.OneHalf, PIXELS_PER_UNIT, 0, SpriteMeshType.FullRect, Vector4.one, false);

                SetThumbnailIntoCache(userId, result);
                failedThumbnails.Remove(userId);
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { HandleCooldownOnException(userId, e); }
            finally
            {
                FinalizeTask(tcs, result, userId);
            }
        }

        private void FinalizeTask(UniTaskCompletionSource<Sprite?> tcs, Sprite? result, string userId)
        {
            currentThumbnailTasks.Remove(userId);
            tcs.TrySetResult(result);
        }

        private void HandleCooldownOnException(string userId, Exception e)
        {
            if (failedThumbnails.TryGetValue(userId, out RequestAttempts requestAttempts))
                if (requestAttempts.CanIncreaseCooldown())
                {
                    requestAttempts.IncreaseCooldown();
                    failedThumbnails[userId] = requestAttempts;
                }
                else
                {
                    ReportData reportData = new ReportData(ReportCategory.PROFILE);
                    ReportHub.LogError(reportData, $"Failed to fetch user thumbnail for the {RequestAttempts.MAX_ATTEMPTS + 1}th time for wallet {userId}");
                    ReportHub.LogException(e, reportData);

                    unsolvableThumbnails.Add(userId);
                    failedThumbnails.Remove(userId);
                }
            else
                failedThumbnails[userId] = RequestAttempts.FirstAttempt();
        }

        private bool TestCooldownCondition(string userId) =>
            !failedThumbnails.TryGetValue(userId, out RequestAttempts requestAttempts) || requestAttempts.CanRetry();

        private void SetThumbnailIntoCache(string userId, Sprite sprite) =>
            thumbnails[userId] = sprite;
    }
}
