using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using DCL.Optimization.ThreadSafePool;
using DCL.Utilities.Extensions;
using DCL.Utility.Types;
using DCL.Web3;
using DCL.WebRequests;
using ECS.Groups;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using ECS.StreamableLoading.DeferredLoading;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine.Pool;

namespace DCL.Profiles
{
    /// <summary>
    ///     Loads profiles in batches:
    ///     <list type="bullet">
    ///         <item>Allows only 1 batch per Lambda URL</item>
    ///         <item>
    ///             Requests are not processed through <see cref="DeferredLoadingSystem" />: profiles are always processed to reduce delays
    ///             (treat it as a reserved capacity per Lambdas URL)
    ///         </item>
    ///     </list>
    /// </summary>
    [UpdateInGroup(typeof(LoadGlobalSystemGroup))]
    public partial class LoadProfilesBatchSystem : LoadSystemBase<ProfilesBatchResult, GetProfilesBatchIntent>
    {
        private static readonly QueryDescription COMPLETED_BATCHES = new QueryDescription().WithAll<StreamableLoadingResult<ProfilesBatchResult>>();

        private static readonly ThreadSafeListPool<Profile> FULL_PROFILE_BATCH_POOL = new (PoolConstants.AVATARS_COUNT, 10);
        private static readonly ThreadSafeListPool<Profile.CompactInfo> COMPACT_PROFILE_BATCH_POOL = new (PoolConstants.AVATARS_COUNT, 10);

        private readonly RealmProfileRepository profileRepository;
        private readonly IWebRequestController webRequestController;
        private readonly ProfilesDebug profilesDebug;

        internal LoadProfilesBatchSystem(World world, RealmProfileRepository profileRepository, IWebRequestController webRequestController, ProfilesDebug profilesDebug) : base(world, NoCache<ProfilesBatchResult, GetProfilesBatchIntent>.INSTANCE)
        {
            this.profileRepository = profileRepository;
            this.webRequestController = webRequestController;
            this.profilesDebug = profilesDebug;
        }

        public override void BeforeUpdate(in float t) =>

            // Clean up resolves requests
            World.Destroy(COMPLETED_BATCHES);

        protected override async UniTask<StreamableLoadingResult<ProfilesBatchResult>> FlowInternalAsync(GetProfilesBatchIntent intention, StreamableLoadingState state, IPartitionComponent partition, CancellationToken ct)
        {
            using GetProfilesBatchIntent _ = intention;

            int pointersCount = intention.Ids.Count;

            switch (intention.Tier)
            {
                case ProfileTier.Kind.Compact:
                {
                    using PooledObject<List<Profile.CompactInfo>> __ = COMPACT_PROFILE_BATCH_POOL.Get(out List<Profile.CompactInfo> batch);

                    Result<IList<Profile.CompactInfo>> result = await ExecuteRequest(batch);

                    // Keep processing on the thread pool

                    if (result is { Success: true })
                    {
                        foreach (Profile.CompactInfo dto in result.Value)
                        {
                            intention.Ids.Remove(dto.UserId);
                            profileRepository.ResolveProfile(dto.UserId, dto);
                        }
                    }

                    break;
                }
                default:
                {
                    using PooledObject<List<Profile>> __ = FULL_PROFILE_BATCH_POOL.Get(out List<Profile> batch);

                    Result<IList<Profile>> result = await ExecuteRequest(batch);

                    // Keep processing on the thread pool

                    if (result is { Success: true })
                    {
                        foreach (Profile dto in result.Value)
                        {
                            intention.Ids.Remove(dto.UserId);
                            profileRepository.ResolveProfile(dto.UserId, dto);
                        }
                    }

                    break;
                }
            }

            int successfullyResolved = pointersCount - intention.Ids.Count;

            if (successfullyResolved > 1)
                profilesDebug.AddAggregated(successfullyResolved);
            else
                profilesDebug.AddNonCombined(successfullyResolved);

            foreach (string unresolvedId in intention.Ids)
                profileRepository.ResolveProfile(unresolvedId, null);

            return new StreamableLoadingResult<ProfilesBatchResult>(new ProfilesBatchResult());

            UniTask<Result<IList<T>>> ExecuteRequest<T>(List<T> batch) =>
                ProfilesRequest.PostAsync(webRequestController, intention.CommonArguments.URL, intention.Ids, batch, ct);
        }
    }
}
