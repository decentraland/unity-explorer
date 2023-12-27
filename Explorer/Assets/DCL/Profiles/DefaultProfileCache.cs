using DCL.Optimization.PerformanceBudgeting;
using System.Collections.Generic;

namespace DCL.Profiles
{
    public class DefaultProfileCache : IProfileCache
    {
        private readonly Dictionary<string, Profile> profiles = new ();

        public int Count => profiles.Count;

        public Profile? Get(string id) =>
            profiles.ContainsKey(id) ? profiles[id] : null;

        public void Set(string id, Profile profile) =>
            profiles[id] = profile;

        public void Unload(IConcurrentBudgetProvider concurrentBudgetProvider, int maxAmount)
        {
            // TODO: clear unused profiles
        }

        public void Remove(string id) =>
            profiles.Remove(id);
    }
}
