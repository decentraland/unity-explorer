using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;
using Object = UnityEngine.Object;

namespace DCL.UI
{
    public class SpriteCache : ISpriteCache
    {
        // TODO unify with `RetryPolicy` of `WebRequestController`
        private struct RequestAttempts
        {
            public const int MAX_ATTEMPTS = 5;
            private const int ATTEMPT_COOLDOWN_SECONDS = 10;

            private int attempts;
            private DateTime nextAvailableAttempt;

            private RequestAttempts(int attempts, DateTime nextAttempt)
            {
                this.attempts = attempts;
                this.nextAvailableAttempt = nextAttempt;
            }

            public bool HasReachedMaxAttempts() =>
                attempts < MAX_ATTEMPTS;

            public void IncreaseCooldown()
            {
                attempts++;
                nextAvailableAttempt = DateTime.Now.AddSeconds(ATTEMPT_COOLDOWN_SECONDS * attempts);
            }

            public bool IsCooldownElapsed() =>
                DateTime.Now >= nextAvailableAttempt;

            public static RequestAttempts FirstAttempt() =>
                new (1, DateTime.Now.AddSeconds(ATTEMPT_COOLDOWN_SECONDS));
        }

        private const int PIXELS_PER_UNIT = 50;

        private readonly IWebRequestController webRequestController;
        private readonly Dictionary<string, Sprite> cachedSprites = new ();
        private readonly Dictionary<string, RequestAttempts> failedSprites = new ();
        private readonly Dictionary<string, UniTaskCompletionSource<Sprite?>> currentSpriteTasks = new ();
        private readonly HashSet<string> unsolvableSprites = new ();

        public SpriteCache(IWebRequestController webRequestController)
        {
            this.webRequestController = webRequestController;
        }

        public Sprite? GetCachedSprite(string imageUrl) =>
            cachedSprites.GetValueOrDefault(imageUrl);

        public async UniTask<Sprite?> GetSpriteAsync(string imageUrl, bool useKtx, RetryPolicy? retryPolicy, CancellationToken ct)
        {
            Sprite? sprite = GetCachedSprite(imageUrl);

            if (sprite != null)
                return sprite;

            // Avoid multiple requests for the same thumbnail
            if (currentSpriteTasks.TryGetValue(imageUrl, out UniTaskCompletionSource<Sprite?> thumbnailTask))
                return await thumbnailTask.Task;

            UniTaskCompletionSource<Sprite?> spriteTaskCompletionSource = new UniTaskCompletionSource<Sprite?>();

            if (currentSpriteTasks.TryAdd(imageUrl, spriteTaskCompletionSource))
                DownloadSpriteAsync(imageUrl, useKtx, retryPolicy, spriteTaskCompletionSource, ct).Forget();

            return await spriteTaskCompletionSource.Task;
        }

        public void AddOrReplaceCachedSprite(string? imageUrl, Sprite imageContent)
        {
            if (imageUrl == null)
                return;

            if (currentSpriteTasks.TryGetValue(imageUrl, out UniTaskCompletionSource<Sprite?>? task))
                task.TrySetCanceled();

            failedSprites.Remove(imageUrl);

            cachedSprites[imageUrl] = imageContent;
        }

        public void Clear()
        {
            foreach (KeyValuePair<string, Sprite> row in cachedSprites)
                Object.Destroy(row.Value);

            cachedSprites.Clear();
            failedSprites.Clear();

            foreach (var runningTask in currentSpriteTasks)
                runningTask.Value.TrySetCanceled();

            currentSpriteTasks.Clear();
        }

        private async UniTaskVoid DownloadSpriteAsync(string imageUrl, bool useKtx, RetryPolicy? retryPolicy, UniTaskCompletionSource<Sprite?> tcs, CancellationToken ct)
        {
            if (URLAddress.EMPTY.Equals(imageUrl)
                || IsUnsolvable(imageUrl)
                || !IsRetryCooldownElapsed(imageUrl))
            {
                currentSpriteTasks.Remove(imageUrl);
                tcs.TrySetResult(null);
                return;
            }

            try
            {
                var ownedTexture = await webRequestController.GetTextureAsync(
                    new CommonArguments(URLAddress.FromString(imageUrl), retryPolicy),
                    new GetTextureArguments(TextureType.Albedo, useKtx),
                    GetTextureWebRequest.CreateTexture(TextureWrapMode.Clamp),
                    ct,
                    ReportCategory.UI,
                    suppressErrors: true
                );

                Texture2D texture = ownedTexture;
                texture.filterMode = FilterMode.Bilinear;

                var result = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                    VectorUtilities.OneHalf, PIXELS_PER_UNIT, 0, SpriteMeshType.FullRect, Vector4.one, false);

                SetSpriteIntoCache(imageUrl, result);
                failedSprites.Remove(imageUrl);
                tcs.TrySetResult(result);
            }
            catch (OperationCanceledException) { tcs.TrySetResult(null); }
            catch (Exception e)
            {
                MarkAsFailed(imageUrl, e);
                tcs.TrySetResult(null);
            }
            finally { currentSpriteTasks.Remove(imageUrl); }
        }

        private void MarkAsFailed(string imageUrl, Exception e)
        {
            if (failedSprites.TryGetValue(imageUrl, out RequestAttempts requestAttempts))
                if (requestAttempts.HasReachedMaxAttempts())
                {
                    requestAttempts.IncreaseCooldown();
                    failedSprites[imageUrl] = requestAttempts;
                }
                else
                {
                    unsolvableSprites.Add(imageUrl);
                    failedSprites.Remove(imageUrl);

                    // This log might be redundant as the exception will be managed from the caller side
                    ReportData reportData = new ReportData(ReportCategory.UI);
                    ReportHub.LogError(reportData, $"Failed to fetch sprite for the {RequestAttempts.MAX_ATTEMPTS + 1}th time for image '{imageUrl}'");
                    ReportHub.LogException(e, reportData);
                }
            else
                failedSprites[imageUrl] = RequestAttempts.FirstAttempt();
        }

        private bool IsRetryCooldownElapsed(string imageUrl)
        {
            if (failedSprites.TryGetValue(imageUrl, out RequestAttempts requestAttempts))
                return requestAttempts.IsCooldownElapsed();

            return true;
        }

        private void SetSpriteIntoCache(string imageUrl, Sprite sprite) =>
            cachedSprites[imageUrl] = sprite;

        private bool IsUnsolvable(string imageUrl) =>
            unsolvableSprites.Contains(imageUrl);
    }
}
