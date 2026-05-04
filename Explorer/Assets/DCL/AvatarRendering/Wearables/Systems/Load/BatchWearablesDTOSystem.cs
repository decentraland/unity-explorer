using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Systems.Abstract;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.GlobalPartitioning;
using DCL.Multiplayer.Connections.DecentralandUrls;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using Global.AppArgs;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.AvatarRendering.Wearables.Systems
{
    /// <summary>
    ///     Batches <see cref="GetWearablesByPointersIntention" /> from different avatars into a single request
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(GlobalDeferredLoadingSystem))] // It is executed before Deferred System to intercept promises
    [UpdateAfter(typeof(ResolveWearablePromisesSystem))] // And right after the promise is actually created
    public partial class BatchWearablesDTOSystem : BatchPointersSystemBase<GetWearableDTOByPointersIntention, WearablesDTOList>
    {
        private readonly IAppArgs appArgs;

        internal BatchWearablesDTOSystem(World world, IDecentralandUrlsSource urlsSource, TimeSpan batchHeartbeat, IAppArgs appArgs) : base(world, batchHeartbeat, urlsSource)
        {
            this.appArgs = appArgs;
        }

        protected override AssetPromise<WearablesDTOList, GetWearableDTOByPointersIntention> CreateAssetPromise(in BatchedPointersIntentions batchedIntentions, CommonLoadingArguments commonLoadingArguments)
        {
            if (!appArgs.TryGetValue(AppArgsFlags.CROSS_ENV_CONTENT_SERVER_URL, out string? zoneUrl))
                return AssetPromise<WearablesDTOList, GetWearableDTOByPointersIntention>.Create(World, new GetWearableDTOByPointersIntention(batchedIntentions.Pointers, commonLoadingArguments), batchedIntentions.Partition);

            var prodPointers = WearableComponentsUtils.POINTERS_POOL.Get();
            var zonePointers = WearableComponentsUtils.POINTERS_POOL.Get();

            foreach (URN urn in batchedIntentions.Pointers)
            {
                if (urn.ToString().Contains(":amoy:", StringComparison.OrdinalIgnoreCase))
                    zonePointers.Add(urn);
                else
                    prodPointers.Add(urn);
            }

            if (zonePointers.Count > 0)
            {
                string zoneEntitiesUrl = $"{zoneUrl!.TrimEnd('/')}/content/entities/active";
                var zoneIntention = new GetWearableDTOByPointersIntention(
                    zonePointers,
                    new CommonLoadingArguments(zoneEntitiesUrl, cancellationTokenSource: new CancellationTokenSource()))
                {
                    OverrideContentServerUrl = zoneUrl
                };
                // Create zone entity without IPartitionComponent so GatherIntentionsForBatch won't re-collect it
                World.Create(AssetPromise<WearablesDTOList, GetWearableDTOByPointersIntention>.Create(World, zoneIntention, batchedIntentions.Partition));
            }
            else
                WearableComponentsUtils.POINTERS_POOL.Release(zonePointers);

            if (prodPointers.Count > 0)
                return AssetPromise<WearablesDTOList, GetWearableDTOByPointersIntention>.Create(World, new GetWearableDTOByPointersIntention(prodPointers, commonLoadingArguments), batchedIntentions.Partition);

            WearableComponentsUtils.POINTERS_POOL.Release(prodPointers);
            return AssetPromise<WearablesDTOList, GetWearableDTOByPointersIntention>.Create(World, new GetWearableDTOByPointersIntention(WearableComponentsUtils.POINTERS_POOL.Get(), commonLoadingArguments), batchedIntentions.Partition);
        }

        protected override IReadOnlyCollection<URN> GetPointers(in AssetPromise<WearablesDTOList, GetWearableDTOByPointersIntention> promise) =>
            promise.LoadingIntention.Pointers;
    }
}
