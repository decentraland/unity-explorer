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
using System;
using System.Collections.Generic;

namespace DCL.AvatarRendering.Wearables.Systems
{
    /// <summary>
    ///     Batches <see cref="GetWearablesByPointersIntention" /> from different avatars into a single request
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(GlobalDeferredLoadingSystem))] // It is executed before Deferred System to intercept promises
    // Finalization systems will destroy the entity with promise
    [UpdateBefore(typeof(FinalizeAssetBundleWearableLoadingSystem))]
    [UpdateBefore(typeof(FinalizeRawWearableLoadingSystem))]
    public partial class BatchWearablesDTOSystem : BatchPointersSystemBase<GetWearableDTOByPointersIntention, WearablesDTOList>
    {
        internal BatchWearablesDTOSystem(World world, IDecentralandUrlsSource urlsSource, TimeSpan batchHeartbeat) : base(world, batchHeartbeat, urlsSource) { }

        protected override AssetPromise<WearablesDTOList, GetWearableDTOByPointersIntention> CreateAssetPromise(in BatchedPointersIntentions batchedIntentions, CommonLoadingArguments commonLoadingArguments) =>
            AssetPromise<WearablesDTOList, GetWearableDTOByPointersIntention>.Create(World, new GetWearableDTOByPointersIntention(batchedIntentions.Pointers, commonLoadingArguments), batchedIntentions.Partition);

        protected override IReadOnlyCollection<URN> GetPointers(in AssetPromise<WearablesDTOList, GetWearableDTOByPointersIntention> promise) =>
            promise.LoadingIntention.Pointers;
    }
}
