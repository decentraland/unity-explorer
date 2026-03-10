using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using ECS;
using ECS.Prioritization.Components;
using System.Threading;
using DCL.AvatarRendering.Thumbnails.Utils;
using DCL.Multiplayer.Connections.DecentralandUrls;
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

        public async UniTask<Sprite> GetAsync(IThumbnailAttachment avatarAttachment, CancellationToken ct)
        {
            if (avatarAttachment.ThumbnailAssetResult is { IsInitialized: true })
                return avatarAttachment.ThumbnailAssetResult.Value.Asset;

            LoadThumbnailsUtils.CreateThumbnailABPromise(
                urlsSource,
                avatarAttachment,
                world,
                PartitionComponent.TOP_PRIORITY,
                CancellationTokenSource.CreateLinkedTokenSource(ct));

            // We dont create an async task from the promise since it needs to be consumed at the proper system, not here
            // The promise's result will eventually get replicated into the avatar attachment
            return await avatarAttachment.WaitForThumbnailAsync(0, ct);
        }
    }
}
