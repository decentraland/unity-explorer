using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Systems.Abstract;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.GlobalPartitioning;
using DCL.Multiplayer.Connections.DecentralandUrls;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Collections.Generic;

namespace DCL.AvatarRendering.Emotes.Load
{
    /// <summary>
    ///     Batches <see cref="GetEmotesDTOByPointersFromRealmIntention" /> from different avatars into a single request
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(GlobalDeferredLoadingSystem))] // It is executed before Deferred System to intercept promises
    [UpdateBefore(typeof(LoadEmotesByPointersSystem))] // 2-in-1 system: Is both for loading and creation, so this system executes next frame before the loading actually kicks in
    // LoadEmotesByPointersSystem: LoadSystemBase => LoadEmotesByPointersSystem: Create a promise => next frame => BatchEmotesDTOSystem: intercept => LoadEmotesByPointersSystem: LoadSystemBase processes the batched request
    public partial class BatchEmotesDTOSystem : BatchPointersSystemBase<GetEmotesDTOByPointersFromRealmIntention, EmotesDTOList>
    {
        internal BatchEmotesDTOSystem(World world, IDecentralandUrlsSource urlsSource, TimeSpan batchHeartbeat) : base(world, batchHeartbeat, urlsSource) { }

        protected override AssetPromise<EmotesDTOList, GetEmotesDTOByPointersFromRealmIntention> CreateAssetPromise(in BatchedPointersIntentions batchedIntentions, CommonLoadingArguments commonLoadingArguments) =>
            AssetPromise<EmotesDTOList, GetEmotesDTOByPointersFromRealmIntention>.Create(World, new GetEmotesDTOByPointersFromRealmIntention(batchedIntentions.Pointers, commonLoadingArguments), batchedIntentions.Partition);

        protected override IReadOnlyCollection<URN> GetPointers(in AssetPromise<EmotesDTOList, GetEmotesDTOByPointersFromRealmIntention> promise) =>
            promise.LoadingIntention.Pointers;
    }
}
