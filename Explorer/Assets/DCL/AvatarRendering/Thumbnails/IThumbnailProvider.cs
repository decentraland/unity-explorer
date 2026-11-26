using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Thumbnails.Utils;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.AvatarRendering.Wearables
{
    public interface IThumbnailProvider
    {
        /// <summary>
        ///     Bounds the wait on a thumbnail load to a sensible UX budget, so requests that stall
        ///     in the streaming pipeline (e.g. budget starvation, lost promises, curl aborts)
        ///     do not leave callers spinning indefinitely.
        /// </summary>
        public const int DEFAULT_TIMEOUT_MS = 30_000;

        UniTask<Sprite> GetAsync(IThumbnailAttachment avatarAttachment, CancellationToken ct, int timeoutMs = DEFAULT_TIMEOUT_MS);
    }

    public static class ThumbnailProviderExtensions
    {
        /// <summary>
        ///     The wait before each retry of a failed attempt. The last delay repeats for as long as the view stays open.
        /// </summary>
        public static readonly IReadOnlyList<int> DEFAULT_RETRY_DELAYS_MS = new[] { 1_000, 2_000, 5_000, 10_000, 20_000, 30_000 };

        /// <summary>
        ///     Loads the thumbnail of a view that stays on screen. An attempt that fails or times out shows the default thumbnail
        ///     and is retried after the next delay, until one succeeds or <paramref name="ct" /> is cancelled because the view closed.
        /// </summary>
        public static async UniTask LoadUntilSucceededAsync(this IThumbnailProvider provider, IThumbnailAttachment attachment, Action<Sprite> setSprite,
            CancellationToken ct, IReadOnlyList<int>? retryDelaysMs = null)
        {
            retryDelaysMs ??= DEFAULT_RETRY_DELAYS_MS;

            for (var attempt = 0;; attempt++)
            {
                Sprite sprite = await provider.GetAsync(attachment, ct);

                if (attachment.ThumbnailAssetResult is { IsInitialized: true, Succeeded: true })
                {
                    setSprite(sprite);
                    return;
                }

                setSprite(LoadThumbnailsUtils.DEFAULT_THUMBNAIL.Sprite);
                await UniTask.Delay(retryDelaysMs[Math.Min(attempt, retryDelaysMs.Count - 1)], cancellationToken: ct);
            }
        }
    }
}
