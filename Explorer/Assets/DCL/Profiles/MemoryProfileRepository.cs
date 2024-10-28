using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
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

        public async UniTask SetAsync(Profile profile, CancellationToken ct) =>
            profileCache.Set(profile.UserId, profile);

        public async UniTask<Profile?> GetAsync(string id, int version, URLDomain? fromCatalyst, CancellationToken ct) =>
            profileCache.Get(id);
    }
}
