using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Profiles
{
    public class CacheProfileRepository : IProfileRepository
    {
        private readonly Dictionary<string, Profile> profiles = new ();

        public async UniTask<Profile?> GetAsync(string id, int version, CancellationToken ct) =>
            profiles.ContainsKey(id) ? profiles[id] : null;

        public void Set(string id, Profile profile) =>
            profiles[id] = profile;

        public void Remove(string id) =>
            profiles.Remove(id);
    }
}
