using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using ECS.Prioritization.Components;
using System.Threading;

namespace DCL.Profiles
{
    public class MemoryProfileRepository : IProfileRepository
    {
        private readonly IProfileCache profileCache;

        public MemoryProfileRepository(IProfileCache profileCache)
        {
            this.profileCache = profileCache;
        }

        public UniTask SetAsync(Profile profile, CancellationToken ct)
        {
            profileCache.Set(profile.UserId, profile);
            return UniTask.CompletedTask;
        }

        public UniTask<ProfileTier?> GetAsync(string id, int version, URLDomain? fromCatalyst, CancellationToken ct, bool delayBatchResolution,
            bool getFromCacheIfPossible, IProfileRepository.BatchBehaviour batchBehaviour, ProfileTier.Kind tier, IPartitionComponent? partition = null) =>
            UniTask.FromResult(profileCache.Get(id));
    }
}
