using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.GlobalPartitioning;
using ECS.Abstract;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using System;
using System.Threading;
using UnityEngine.Assertions;
using Utility;

namespace DCL.Profiles
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(GlobalDeferredLoadingSystem))]
    public partial class PrepareProfilesBatchSystem : BaseUnityLoopSystem
    {
        private readonly TimeSpan batchHeartbeat;
        private readonly RealmProfileRepository profileRepository;

        private readonly CancellationTokenSource cts = new ();

        private DateTime nextDispatch = DateTime.MinValue;

        internal PrepareProfilesBatchSystem(World world, TimeSpan batchHeartbeat, RealmProfileRepository profileRepository) : base(world)
        {
            this.batchHeartbeat = batchHeartbeat;
            this.profileRepository = profileRepository;
        }

        protected override void Update(float t)
        {
            if (nextDispatch < DateTime.Now)
                return;

            // Create a separate request for each Lambdas URL
            foreach (ProfilesBatchRequest batch in profileRepository.ConsumePendingBatch())
            {
                Assert.IsTrue(batch.PendingRequests.Count > 0);

                IPartitionComponent partition = PartitionComponent.MIN_PRIORITY;

                var intent = new GetProfilesBatchIntent(profileRepository.PostUrl(batch.LambdasUrl), batch.LambdasUrl, cts);

                foreach ((string? userId, ProfilesBatchRequest.Input input) in batch.PendingRequests)
                {
                    intent.Ids.Add(userId);

                    // Set the closest partition
                    if (BucketBasedComparer.INSTANCE.Compare(partition, input.Partition) <= 0)
                        partition = input.Partition;
                }

                // Create an entity with promise
                AssetPromise<ProfilesBatchResult, GetProfilesBatchIntent>.Create(World, intent, partition);
            }

            nextDispatch = DateTime.Now + batchHeartbeat;
        }

        protected override void OnDispose() =>
            cts.SafeCancelAndDispose();
    }
}
