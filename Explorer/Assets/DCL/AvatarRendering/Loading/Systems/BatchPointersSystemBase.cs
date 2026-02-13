using Arch.Core;
using Arch.System;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Multiplayer.Connections.DecentralandUrls;
using ECS.Abstract;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Collections.Generic;
using System.Threading;
using Utility;

namespace DCL.AvatarRendering.Loading.Systems.Abstract
{
    public abstract partial class BatchPointersSystemBase<TIntention, TAsset> : BaseUnityLoopSystem where TIntention: IAssetIntention, IEquatable<TIntention>
    {
        private readonly TimeSpan batchHeartbeat;
        private DateTime nextDispatch = DateTime.MinValue;

        private readonly CancellationTokenSource cts = new ();
        private readonly IDecentralandUrlsSource urlsSource;

        private BatchedPointersIntentions currentBatch = BatchedPointersIntentions.Create();

        protected BatchPointersSystemBase(World world, TimeSpan batchHeartbeat, IDecentralandUrlsSource urlsSource) : base(world)
        {
            this.batchHeartbeat = batchHeartbeat;
            this.urlsSource = urlsSource;
        }

        protected override void Update(float t)
        {
            // Gather intentions for the batch immediately so they are not processed individually
            GatherIntentionsForBatchQuery(World, ref currentBatch);

            if (nextDispatch > DateTime.Now)
                return;

            // The time is right - launch the batch

            if (currentBatch.Pointers.Count == 0)
                currentBatch.Dispose();
            else
            {
                // Create a new entity
                World.Create(CreateAssetPromise(currentBatch, new CommonLoadingArguments(urlsSource.Url(DecentralandUrl.EntitiesActive), cancellationTokenSource: cts)));

                // The batch will be finalized by FinalizeWearableLoadingSystemBase - no special actions are needed
            }

            nextDispatch = DateTime.Now + batchHeartbeat;

            // Recreate the batch
            currentBatch = BatchedPointersIntentions.Create();
        }

        protected abstract AssetPromise<TAsset, TIntention> CreateAssetPromise(in BatchedPointersIntentions batchedIntentions, CommonLoadingArguments commonLoadingArguments);

        protected abstract IReadOnlyCollection<URN> GetPointers(in AssetPromise<TAsset, TIntention> promise);

        [Query]
        private void GatherIntentionsForBatch([Data] ref BatchedPointersIntentions batch, Entity entity, ref AssetPromise<TAsset, TIntention> promise, IPartitionComponent partition)
        {
            batch.Pointers.AddRange(GetPointers(in promise));

            // Assign the closest partition
            // There is no much sense to group further by partition as we aim for the minimum number of requests

            if (BucketBasedComparer.INSTANCE.Compare(batch.Partition, partition) > 0)
                batch.Partition = partition;

            // Remove the original promise as it's no longer needed -> there is no logic relying on the presence of the promise itself:
            // Wearables resolution is synced via the cache
            // NOTE: don't generalize it: it's only true for the wearables/emotes flow
            promise.ForgetLoading(World);
            World.Destroy(entity);
        }

        protected override void OnDispose() =>
            cts.SafeCancelAndDispose();
    }
}
