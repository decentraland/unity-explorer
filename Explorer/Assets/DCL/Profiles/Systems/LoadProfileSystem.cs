using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Profiles.Helpers;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using System;
using System.Threading;

namespace DCL.Profiles
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.PROFILE)]
    public partial class LoadProfileSystem : LoadSystemBase<ProfileData, GetProfileIntention>
    {
        private readonly IProfileRepository profileRepository;

        public LoadProfileSystem(World world,
            IStreamableCache<ProfileData, GetProfileIntention> cache,
            IProfileRepository profileRepository)
            : base(world, cache)
        {
            this.profileRepository = profileRepository;
        }

        protected override void Update(float t)
        {
            base.Update(t);

            ResolveProfilePromiseQuery(World);
        }

        protected override async UniTask<StreamableLoadingResult<ProfileData>> FlowInternalAsync(GetProfileIntention intention,
            IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
        {
            Profile? profile = await profileRepository.GetAsync(intention.ProfileId, intention.Version, ct);

            if (profile == null)
                throw new Exception($"Profile not found {intention.ProfileId}");

            ProfileUtils.CreateProfilePicturePromise(profile, World, partition);
            return new StreamableLoadingResult<ProfileData>(new ProfileData(profile));
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
            else ReportHub.LogException(result.Exception, GetReportData());

            World.Remove<AssetPromise<Profile, GetProfileIntention>>(entity);
        }
    }
}
