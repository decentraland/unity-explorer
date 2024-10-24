using DCL.Optimization.PerformanceBudgeting;
using DCL.Profiling;
using System.Collections.Generic;

namespace DCL.Profiles
{
    public class DefaultProfileCache : IProfileCache
    {
        private readonly Dictionary<string, Profile> profiles = new ();

        public Profile? Get(string id) =>
            profiles.ContainsKey(id) ? profiles[id] : null;

        public void Set(string id, Profile profile)
        {
            if (profiles.TryGetValue(id, out Profile existingProfile))
                if (existingProfile != profile)
                    existingProfile.Dispose();

            profiles[id] = profile;

            UpdateProfilingCounter();
        }

        public void Unload(IPerformanceBudget concurrentBudgetProvider, int maxAmount)
        {
            // TODO: clear unused profiles
            //When done, remember to add the `UnloadImmediate` call in CacheCleaner

            UpdateProfilingCounter();
        }

        public void Remove(string id)
        {
            if (profiles.TryGetValue(id, out Profile existingProfile))
                existingProfile.Dispose();

            profiles.Remove(id);

            UpdateProfilingCounter();
        }

        private void UpdateProfilingCounter()
        {
            ProfilingCounters.ProfilesInCache.Value = profiles.Count;
        }
    }
}
