using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using System;
using System.Threading;
using Utility.Multithreading;

namespace DCL.Profiles
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.PROFILE)]
    public partial class LoadProfileSystem : LoadSystemBase<Profile, GetProfileIntention>
    {
        private readonly IProfileRepository profileRepository;

        public LoadProfileSystem(World world,
            IStreamableCache<Profile, GetProfileIntention> cache,
            MutexSync mutexSync,
            IProfileRepository profileRepository)
            : base(world, cache, mutexSync)
        {
            this.profileRepository = profileRepository;
        }

        protected override async UniTask<StreamableLoadingResult<Profile>> FlowInternalAsync(GetProfileIntention intention,
            IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
        {
            try
            {
                Profile? profile = await profileRepository.GetAsync(intention.ProfileId, intention.Version, ct);

                return profile == null
                    ? new StreamableLoadingResult<Profile>(new Exception($"Profile does not exist {intention.ProfileId}"))
                    : new StreamableLoadingResult<Profile>(profile);
            }
            catch (Exception e) { return new StreamableLoadingResult<Profile>(e); }
        }
    }
}
