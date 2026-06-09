using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.Exceptions;
using DCL.AvatarRendering.Thumbnails.Utils;
using DCL.Multiplayer.Connections.DecentralandUrls;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.AvatarRendering.Wearables
{
    public class ECSThumbnailProvider : IThumbnailProvider
    {
        private readonly IDecentralandUrlsSource urlsSource;
        private readonly World world;

        public ECSThumbnailProvider(IDecentralandUrlsSource urlsSource, World world)
        {
            this.urlsSource = urlsSource;
            this.world = world;
        }

        public async UniTask<Sprite> GetAsync(IThumbnailAttachment avatarAttachment, CancellationToken ct, int timeoutMs = IThumbnailProvider.DEFAULT_TIMEOUT_MS)
        {
            if (avatarAttachment.ThumbnailAssetResult is { IsInitialized: true } existing)
            {
                if (existing.Succeeded)
                    return existing.Asset;

                // A previous attempt was cancelled (e.g. page change). Clear so a fresh promise
                // can spawn below. Sticky failures keep the slot and fall through to throw.
                if (existing.Cancelled)
                    avatarAttachment.ThumbnailAssetResult = null;
                else
                    throw new ThumbnailLoadFailedException();
            }

            CancellationTokenSource promiseCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            LoadThumbnailsUtils.CreateThumbnailABPromise(
                urlsSource,
                avatarAttachment,
                world,
                PartitionComponent.TOP_PRIORITY,
                promiseCts);

            using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(timeoutMs);

            try
            {
                // We dont create an async task from the promise since it needs to be consumed at the proper system, not here
                // The promise's result will eventually get replicated into the avatar attachment
                return await avatarAttachment.WaitForThumbnailAsync(0, timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // Timed out: cancel the underlying promise so the resolver cleans it up, and record
                // a sticky Failed on the attachment so subsequent calls don't immediately re-attempt
                // a load that we already gave up on. (The resolver will see IsCancellationRequested
                // but skip overwriting because the slot is now Failed, not Cancelled.)
                promiseCts.Cancel();
                avatarAttachment.ThumbnailAssetResult = StreamableLoadingResult<SpriteData>.WithFallback.Failed();

                throw new ThumbnailLoadFailedException($"Thumbnail load timed out after {timeoutMs}ms");
            }
        }
    }
}
