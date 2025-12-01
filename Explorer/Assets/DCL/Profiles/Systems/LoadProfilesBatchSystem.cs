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
        // JSON structure overhead for {"ids":[...]}
        private const int BASE_JSON_OVERHEAD = 10; // {"ids":[]}

        // Per-string overhead in JSON
        private const int QUOTES_PER_STRING = 2; // Opening and closing quotes
        private const int COMMA_SEPARATOR = 1; // Comma between array elements

        private static readonly QueryDescription COMPLETED_BATCHES = new QueryDescription().WithAll<StreamableLoadingResult<ProfilesBatchResult>>();

        private static readonly ThreadSafeListPool<GetProfileJsonRootDto> BATCH_POOL = new (PoolConstants.AVATARS_COUNT, 10);

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

        /// <summary>
        ///     Calculates the exact buffer size needed for a JSON payload with ETH wallet IDs.
        ///     Format: {"ids":["0x1234...","0x5678...",...]}.
        /// </summary>
        /// <param name="idCount">Number of ETH wallet addresses</param>
        /// <returns>Exact byte size needed for the JSON payload</returns>
        private static int CalculateExactSize(int idCount)
        {
            if (idCount <= 0)
                return BASE_JSON_OVERHEAD; // Empty array: {"ids":[]}

            // Base structure: {"ids":[]}
            int totalSize = BASE_JSON_OVERHEAD;

            // Each ID contributes:
            // - 2 bytes for quotes: ""
            // - addressLength bytes for the actual address
            // - 1 byte for comma (except last element)
            int bytesPerId = QUOTES_PER_STRING + Web3Address.ETH_ADDRESS_LENGTH;
            totalSize += bytesPerId * idCount;

            // Add commas between elements (idCount - 1 commas)
            totalSize += (idCount - 1) * COMMA_SEPARATOR;

            return totalSize;
        }

        protected override async UniTask<StreamableLoadingResult<ProfilesBatchResult>> FlowInternalAsync(GetProfilesBatchIntent intention, StreamableLoadingState state, IPartitionComponent partition, CancellationToken ct)
        {
            using GetProfilesBatchIntent _ = intention;

            using var uploadHandler = new BufferedStringUploadHandler(CalculateExactSize(intention.Ids.Count));

            uploadHandler.WriteString("{\"ids\":[");

            int i = 0;

            foreach (string id in intention.Ids)
            {
                uploadHandler.WriteJsonString(id);

                if (i != intention.Ids.Count - 1)
                    uploadHandler.WriteChar(',');
                i++;
            }

            uploadHandler.WriteString("]}");

            using PooledObject<List<GetProfileJsonRootDto>> __ = BATCH_POOL.Get(out List<GetProfileJsonRootDto> batch);

            Result<List<GetProfileJsonRootDto>> result = await webRequestController.PostAsync(
                                                                                        intention.CommonArguments.URL,
                                                                                        GenericPostArguments.CreateUploadHandler(uploadHandler.CreateUploadHandler(), GenericPostArguments.JSON),
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

                    dto.Dispose();
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
