using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.CommunicationData.URLHelpers;
using DCL.Diagnostics;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.UI
{
    public class SpriteCache : ISpriteCache
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
        private readonly Dictionary<Uri, Sprite> cachedSprites = new (OriginalStringURLComparer.Instance);
        private readonly Dictionary<Uri, RequestAttempts> failedSprites = new (OriginalStringURLComparer.Instance);
        private readonly Dictionary<Uri, UniTaskCompletionSource<Sprite?>> currentSpriteTasks = new (OriginalStringURLComparer.Instance);
        private readonly HashSet<Uri> unsolvableSprites = new (OriginalStringURLComparer.Instance);

        public SpriteCache(IWebRequestController webRequestController)
        {
            this.webRequestController = webRequestController;
        }

        public Sprite? GetCachedSprite(Uri imageUrl) =>
            cachedSprites.GetValueOrDefault(imageUrl);

        public async UniTask<Sprite?> GetSpriteAsync(Uri imageUrl, CancellationToken ct) =>
            await GetSpriteAsync(imageUrl, false, ct);

        public async UniTask<Sprite?> GetSpriteAsync(Uri imageUrl, bool useKtx, CancellationToken ct)
        {
            Sprite? sprite = GetCachedSprite(imageUrl);
            if (sprite != null)
                return sprite;

            //Avoid multiple requests for the same thumbnail
            if (currentSpriteTasks.TryGetValue(imageUrl, out UniTaskCompletionSource<Sprite?> thumbnailTask))
                return await thumbnailTask.Task;

            UniTaskCompletionSource<Sprite?> spriteTaskCompletionSource = new UniTaskCompletionSource<Sprite?>();
            if (currentSpriteTasks.TryAdd(imageUrl, spriteTaskCompletionSource))
                DownloadSpriteAsync(imageUrl, useKtx, spriteTaskCompletionSource, ct).Forget();

            return await spriteTaskCompletionSource.Task;
        }

        public void AddOrReplaceCachedSprite(Uri? imageUrl, Sprite imageContent)
        {
            if (imageUrl == null)
                return;

            if(currentSpriteTasks.TryGetValue(imageUrl, out UniTaskCompletionSource<Sprite?>? task))
                task.TrySetCanceled();

            failedSprites.Remove(imageUrl);

            cachedSprites[imageUrl] = imageContent;
        }

        public void Clear()
        {
            foreach (KeyValuePair<Uri, Sprite> row in cachedSprites)
                GameObject.Destroy(row.Value);

            cachedSprites.Clear();
            failedSprites.Clear();

            foreach (var runningTask in currentSpriteTasks)
                runningTask.Value.TrySetCanceled();

            currentSpriteTasks.Clear();
        }

        private async UniTaskVoid DownloadSpriteAsync(Uri? imageUrl, bool useKtx, UniTaskCompletionSource<Sprite?> tcs, CancellationToken ct)
        {
            Sprite? result = null;

            if (imageUrl == null)
            {
                FinalizeTask(tcs, result, imageUrl);
                return;
            }

            if (unsolvableSprites.Contains(imageUrl) || !TestCooldownCondition(imageUrl))
            {
                FinalizeTask(tcs, result, imageUrl);
                return;
            }

            try
            {
                IOwnedTexture2D ownedTexture = await webRequestController.GetTextureAsync(
                    new CommonArguments(imageUrl),
                    new GetTextureArguments(TextureType.Albedo, useKtx),
                    ReportCategory.UI,
                    suppressErrors: true
                                                                          )
                                                                         .CreateTextureAsync(TextureWrapMode.Clamp, ct: ct);

                Texture2D texture = ownedTexture.Texture;
                texture.filterMode = FilterMode.Bilinear;

                result = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                    VectorUtilities.OneHalf, PIXELS_PER_UNIT, 0, SpriteMeshType.FullRect, Vector4.one, false);

                SetSpriteIntoCache(imageUrl, result);
                failedSprites.Remove(imageUrl);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                HandleCooldownOnException(imageUrl, e);
            }
            finally
            {
                FinalizeTask(tcs, result, imageUrl);
            }
        }

        private void FinalizeTask(UniTaskCompletionSource<Sprite?> tcs, Sprite? result, Uri? imageUrl)
        {
            if (imageUrl != null)
                currentSpriteTasks.Remove(imageUrl);
            tcs.TrySetResult(result);
        }

        private void HandleCooldownOnException(Uri imageUrl, Exception e)
        {
            if (failedSprites.TryGetValue(imageUrl, out RequestAttempts requestAttempts))
                if (requestAttempts.CanIncreaseCooldown())
                {
                    requestAttempts.IncreaseCooldown();
                    failedSprites[imageUrl] = requestAttempts;
                }
                else
                {
                    ReportData reportData = new ReportData(ReportCategory.UI);
                    ReportHub.LogError(reportData, $"Failed to fetch sprite for the {RequestAttempts.MAX_ATTEMPTS + 1}th time for image '{imageUrl}'");
                    ReportHub.LogException(e, reportData);

                    unsolvableSprites.Add(imageUrl);
                    failedSprites.Remove(imageUrl);
                }
            else
                failedSprites[imageUrl] = RequestAttempts.FirstAttempt();
        }

        private bool TestCooldownCondition(Uri imageUrl) =>
            !failedSprites.TryGetValue(imageUrl, out RequestAttempts requestAttempts) || requestAttempts.CanRetry();

        private void SetSpriteIntoCache(Uri imageUrl, Sprite sprite) =>
            cachedSprites[imageUrl] = sprite;
    }
}
