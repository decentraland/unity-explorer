using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Optimization.Pools;
using DCL.Optimization.ThreadSafePool;
using ECS.Prioritization.Components;
using System;
using System.Collections.Generic;

namespace DCL.Profiles
{
    public struct ProfilesBatchRequest
    {
        public readonly struct Input
        {
            /// <summary>
            ///     Original Completion Source, when it is fired the original request gets continued
            /// </summary>
            public readonly UniTaskCompletionSource<ProfileTier?> Cs;

            /// <summary>
            ///     If the request originates from UI it will be always <see cref="PartitionComponent.TOP_PRIORITY" />
            /// </summary>
            public readonly IPartitionComponent Partition;

            public Input(UniTaskCompletionSource<ProfileTier?> cs, IPartitionComponent partition)
            {
                Cs = cs;
                Partition = partition;
            }
        }

        private static readonly ThreadSafeDictionaryPool<string, Input> POOL
            = new (PoolConstants.AVATARS_COUNT, PoolConstants.AVATARS_COUNT * 5, StringComparer.OrdinalIgnoreCase);

        public readonly URLDomain LambdasUrl;
        public readonly Dictionary<string, Input> PendingRequests;
        public ProfileTier.Kind Tier;

        private ProfilesBatchRequest(URLDomain lambdasUrl, Dictionary<string, Input> pendingRequests, ProfileTier.Kind tier)
        {
            LambdasUrl = lambdasUrl;
            PendingRequests = pendingRequests;
            Tier = tier;
        }

        internal static ProfilesBatchRequest Create(URLDomain lambdasUrl, ProfileTier.Kind tier) =>
            new (lambdasUrl, POOL.Get(), tier);

        /// <summary>
        ///     The batch is disposed of when the last request in the batch is executed
        /// </summary>
        public void Dispose() =>
            POOL.Release(PendingRequests);
    }
}
