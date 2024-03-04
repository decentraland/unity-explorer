using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common;
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

        protected override void Update(float t)
        {
            base.Update(t);

            ResolveProfilePromiseQuery(World);
        }

        protected override async UniTask<StreamableLoadingResult<Profile>> FlowInternalAsync(GetProfileIntention intention,
            IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
        {
            Profile? profile = await profileRepository.GetAsync(intention.ProfileId, intention.Version, ct);

            if (profile == null)
                throw new Exception($"Profile not found {intention.ProfileId}");

            return new StreamableLoadingResult<Profile>(profile);
        }

        [Query]
        private void ResolveProfilePromise(in Entity entity, ref AssetPromise<Profile, GetProfileIntention> promise)
        {
            if (!promise.TryConsume(World, out StreamableLoadingResult<Profile> result)) return;

            if (result.Succeeded)
            {
                result.Asset.IsDirty = true;

                if (World.Has<Profile>(entity))
                    World.Set(entity, result.Asset);
                else
                    World.Add(entity, result.Asset);
            }
            else ReportHub.LogException(result.Exception, GetReportCategory());

            World.Remove<AssetPromise<Profile, GetProfileIntention>>(entity);
        }
    }
}
