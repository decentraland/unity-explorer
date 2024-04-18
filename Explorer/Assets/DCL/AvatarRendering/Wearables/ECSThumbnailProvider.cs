using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using ECS;
using ECS.Prioritization.Components;
using System.Threading;
using UnityEngine;
using ThumbnailPromise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.Texture2D, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace DCL.AvatarRendering.Wearables
{
    public class ECSThumbnailProvider : IThumbnailProvider
    {
        private const int RESOLUTION_FREQUENCY_MS = 250;

        private readonly IRealmData realmData;
        private readonly World world;

        public ECSThumbnailProvider(IRealmData realmData,
            World world)
        {
            this.realmData = realmData;
            this.world = world;
        }

        public async UniTask<Sprite?> GetAsync(IAvatarAttachment avatarAttachment, CancellationToken ct)
        {
            if (avatarAttachment.ThumbnailAssetResult != null)
                return avatarAttachment.ThumbnailAssetResult.Value.Asset;

            var alreadyRunningPromise = false;

            world.Query(in new QueryDescription().WithAll<IAvatarAttachment, ThumbnailPromise, IPartitionComponent>(),
                entity =>
                {
                    ref IAvatarAttachment a = ref world.Get<IAvatarAttachment>(entity);

                    if (a.GetThumbnail().Equals(avatarAttachment.GetThumbnail()))
                        alreadyRunningPromise = true;
                });

            if (!alreadyRunningPromise)
                WearableComponentsUtils.CreateWearableThumbnailPromise(realmData, avatarAttachment, world, PartitionComponent.TOP_PRIORITY);

            // We dont create an async task from the promise since it needs to be consumed at the proper system, not here
            // The promise's result will eventually get replicated into the avatar attachment
            do await UniTask.Delay(RESOLUTION_FREQUENCY_MS, cancellationToken: ct);
            while (avatarAttachment.ThumbnailAssetResult == null);

            return avatarAttachment.ThumbnailAssetResult!.Value.Asset;
        }
    }
}
