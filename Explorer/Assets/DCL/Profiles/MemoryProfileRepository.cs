using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using ECS.Prioritization.Components;
using System;
using System.Collections.Generic;
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

        public UniTask<List<Profile>> GetAsync(IReadOnlyList<string> ids, CancellationToken ct, URLDomain? fromCatalyst = null)
        {
            var list = new List<Profile>(ids.Count);

            foreach (string id in ids)
            {
                Profile? profile = profileCache.Get(id);

                if (profile != null)
                    list.Add(profile);
            }

            return UniTask.FromResult(list);
        }

        public UniTask<Profile?> GetAsync(string id, int version, URLDomain? fromCatalyst, CancellationToken ct, bool getFromCacheIfPossible = true,
            IProfileRepository.BatchBehaviour batchBehaviour = IProfileRepository.BatchBehaviour.DEFAULT, IPartitionComponent? partition = null) =>
            UniTask.FromResult(profileCache.Get(id));
    }
}
