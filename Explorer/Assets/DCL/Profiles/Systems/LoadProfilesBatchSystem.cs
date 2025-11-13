using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using DCL.Optimization.ThreadSafePool;
using DCL.Utilities.Extensions;
using DCL.Utility.Types;
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

        private static readonly ThreadSafeListPool<GetProfileJsonRootDto> BATCH_POOL = new (PoolConstants.AVATARS_COUNT, 10);

        private readonly RealmProfileRepository profileRepository;
        private readonly IWebRequestController webRequestController;
        private readonly ProfilesDebug profilesDebug;

        private readonly StringBuilder bodyBuilder = new ();

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

            bodyBuilder.Clear();

            bodyBuilder.Append("{\"ids\":[");

            int i = 0;

            foreach (string id in intention.Ids)
            {
                bodyBuilder.Append('\"');
                bodyBuilder.Append(id);
                bodyBuilder.Append('\"');

                if (i != intention.Ids.Count - 1)
                    bodyBuilder.Append(",");

                i++;
            }

            bodyBuilder.Append("]}");

            using PooledObject<List<GetProfileJsonRootDto>> __ = BATCH_POOL.Get(out List<GetProfileJsonRootDto> batch);

            var result = await webRequestController.PostAsync(
                                                        intention.CommonArguments.URL,
                                                        GenericPostArguments.CreateJson(bodyBuilder.ToString()),
                                                        ct,
                                                        ReportCategory.PROFILE)
                                                   .OverwriteFromNewtonsoftJsonAsync(batch, WRThreadFlags.SwitchToThreadPool, serializerSettings: RealmProfileRepository.SERIALIZER_SETTINGS)
                                                   .SuppressToResultAsync(ReportCategory.PROFILE);

            // Keep processing on the thread pool

            if (result is { Success: true })
            {
                int successfullyResolved = 0;

                foreach (GetProfileJsonRootDto dto in result.Value)
                {
                    ProfileJsonDto? profileDto = dto.FirstProfileDto();

                    if (profileDto == null) continue;

                    intention.Ids.Remove(profileDto.userId);
                    profileRepository.ResolveProfile(profileDto.userId, profileDto);
                    successfullyResolved++;
                }

                if (successfullyResolved > 1)
                    profilesDebug.AddAggregated(successfullyResolved);
                else
                    profilesDebug.AddNonCombined(successfullyResolved);
            }

            foreach (string unresolvedId in intention.Ids)
                profileRepository.ResolveProfile(unresolvedId, null);

            return new StreamableLoadingResult<ProfilesBatchResult>(new ProfilesBatchResult());
        }
    }
}
