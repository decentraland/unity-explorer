using DCL.Optimization.PerformanceBudgeting;
using DCL.Profiling;
using System;
using System.Collections.Generic;

namespace DCL.Profiles
{
    public class DefaultProfileCache : IProfileCache
    {
        private readonly Dictionary<string, Profile> profiles = new ();
        private readonly Dictionary<string, string> userNameToIdMap = new ();

        public Profile? Get(string id) =>
            profiles.ContainsKey(id) ? profiles[id] : null;

        public bool TryGet(string id, out Profile profile) =>
            profiles.TryGetValue(id, out profile);

        public Profile? GetByUserName(string userName)
        {
            if (userNameToIdMap.TryGetValue(userName, out string? profileId))
                return profiles[profileId];

            return null;
        }

        public void Set(string id, Profile profile)
        {
            if (profiles.TryGetValue(id, out Profile existingProfile))
                if (existingProfile != profile)
                    existingProfile.Dispose();

            profiles[id] = profile;
            userNameToIdMap[profile.DisplayName] = id;

            UpdateProfilingCounter();
        }

        public void Unload(IPerformanceBudget concurrentBudgetProvider, int maxAmount)
        {
            // TODO: clear unused profiles

            UpdateProfilingCounter();
        }

        public void Remove(string id)
        {
            if (profiles.TryGetValue(id, out Profile existingProfile))
            {
                userNameToIdMap.Remove(existingProfile.DisplayName);
                existingProfile.Dispose();
            }

            profiles.Remove(id);

            UpdateProfilingCounter();
        }

        private void UpdateProfilingCounter()
        {
            ProfilingCounters.ProfilesInCache.Value = profiles.Count;
        }
    }
}
