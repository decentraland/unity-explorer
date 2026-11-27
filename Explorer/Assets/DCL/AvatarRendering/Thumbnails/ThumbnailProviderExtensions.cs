using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Thumbnails.Utils;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.AvatarRendering.Wearables
{
    public static class ThumbnailProviderExtensions
    {
        /// <summary>
        ///     The wait before each retry of a failed attempt. The load stops retrying after the last one.
        /// </summary>
        private static readonly IReadOnlyList<int> DEFAULT_RETRY_DELAYS_MS = new[] { 1_000, 2_000, 5_000, 10_000, 20_000, 30_000 };

        /// <summary>
        ///     Loads the thumbnail and passes it to <paramref name="setSprite" />. A failed or timed-out attempt passes the default thumbnail
        ///     and is retried after the next delay, until one succeeds, the delays run out or <paramref name="ct" /> is cancelled.
        /// </summary>
        public static UniTask LoadWithRetriesAsync(this IThumbnailProvider provider, IThumbnailAttachment attachment, Action<Sprite> setSprite, CancellationToken ct) =>
            provider.LoadWithRetriesAsync(attachment, setSprite, ct, DEFAULT_RETRY_DELAYS_MS);

        internal static async UniTask LoadWithRetriesAsync(this IThumbnailProvider provider, IThumbnailAttachment attachment, Action<Sprite> setSprite,
            CancellationToken ct, IReadOnlyList<int> retryDelaysMs)
        {
            for (var attempt = 0;; attempt++)
            {
                Sprite sprite = await provider.GetAsync(attachment, ct);

                if (attachment.ThumbnailAssetResult is { Succeeded: true })
                {
                    setSprite(sprite);
                    return;
                }

                setSprite(LoadThumbnailsUtils.DEFAULT_THUMBNAIL.Sprite);

                if (attempt >= retryDelaysMs.Count)
                    return;

                await UniTask.Delay(retryDelaysMs[attempt], cancellationToken: ct);
            }
        }
    }
}
