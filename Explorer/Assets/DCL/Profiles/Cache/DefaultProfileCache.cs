using DCL.Optimization.PerformanceBudgeting;
using DCL.Profiling;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine.Assertions;

namespace DCL.Profiles
{
    public class DefaultProfileCache : IProfileCache
    {
        private readonly ConcurrentDictionary<string, ProfileTier> profiles = new (StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, string> userNameToIdMap = new ();

        public ProfileTier? Get(string id) =>
            profiles.GetValueOrDefault(id);

        public bool TryGet(string id, ProfileTier.Kind tier, out ProfileTier profile)
        {
            if (profiles.TryGetValue(id, out profile) && profile.GetKind() >= tier)
                return true;

            profile = default(ProfileTier);
            return false;
        }

        public ProfileTier? GetByUserName(string userName)
        {
            if (userNameToIdMap.TryGetValue(userName, out string? profileId))
                return profiles[profileId];

            return null;
        }

        public void Set(string id, ProfileTier profile)
        {
            if (profiles.TryGetValue(id, out ProfileTier existingProfile))
            {
                // The cached full profile can be never replaced by the compact version
                Assert.IsTrue(profile.GetKind() >= existingProfile.GetKind(), "profile.GetKind() >= existingProfile.GetKind()");

                if (existingProfile != profile)
                {
                    InheritDynamicState(existingProfile, profile);
                    existingProfile.Dispose();
                }
            }

            profiles[id] = profile;
            userNameToIdMap[profile.DisplayName] = id;

            UpdateProfilingCounter();
        }

        private static void InheritDynamicState(ProfileTier from, ProfileTier to)
        {
            // Only inherit if the snapshot URL hasn't changed — otherwise the old picture/promise is stale.
            if (from.FaceSnapshotUrl != to.FaceSnapshotUrl)
                return;

            // Detach on the source after transfer so Dispose doesn't cancel or dereference work we just moved.
            to.ProfilePicture = from.ProfilePicture;
            from.ProfilePicture = null;

            if (from.IsFull(out Profile? fromFull) && to.IsFull(out Profile? toFull))
            {
                toFull.PicturePromise = fromFull.PicturePromise;
                fromFull.PicturePromise = null;
            }
        }

        public void Unload(IPerformanceBudget concurrentBudgetProvider, int maxAmount)
        {
            // TODO: clear unused profiles

            UpdateProfilingCounter();
        }

        public void Remove(string id)
        {
            if (profiles.TryGetValue(id, out ProfileTier existingProfile))
            {
                userNameToIdMap.TryRemove(existingProfile.DisplayName, out _);
                existingProfile.Dispose();
            }

            profiles.TryRemove(id, out _);

            UpdateProfilingCounter();
        }

        private void UpdateProfilingCounter()
        {
            ProfilingCounters.ProfilesInCache.Value = profiles.Count;
        }
    }
}
