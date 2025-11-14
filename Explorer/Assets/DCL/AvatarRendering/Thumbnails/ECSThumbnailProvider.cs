using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using ECS;
using ECS.Prioritization.Components;
using System.Threading;
using DCL.AvatarRendering.Thumbnails.Utils;
using UnityEngine;

namespace DCL.AvatarRendering.Wearables
{
    public class ECSThumbnailProvider : IThumbnailProvider
    {
        private readonly IRealmData realmData;
        private readonly World world;

        public ECSThumbnailProvider(IRealmData realmData, World world)
        {
            this.realmData = realmData;
            this.world = world;
        }

        public async UniTask<Sprite> GetAsync(IThumbnailAttachment avatarAttachment, CancellationToken ct)
        {
            if (avatarAttachment.ThumbnailAssetResult is { IsInitialized: true })
                return avatarAttachment.ThumbnailAssetResult.Value.Asset;

            LoadThumbnailsUtils.CreateThumbnailABPromiseAsync(
                realmData,
                avatarAttachment,
                world,
                PartitionComponent.TOP_PRIORITY,
                CancellationTokenSource.CreateLinkedTokenSource(ct))
                .Forget();

            // We dont create an async task from the promise since it needs to be consumed at the proper system, not here
            // The promise's result will eventually get replicated into the avatar attachment
            return await avatarAttachment.WaitForThumbnailAsync(0, ct);
        }
    }
}
