using CommunicationData.URLHelpers;
using DCL.Diagnostics;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace DCL.Profiles.Self
{
    /// <summary>Wearable URNs faked as equipped on the self profile, so one can be worn without owning it. Fixed at construction from the launch arguments or the debug settings.</summary>
    public class ForcedWearables
    {
        private readonly HashSet<URN> wearables = new ();

        public bool Any => wearables.Count > 0;

        public ForcedWearables(IEnumerable<URN>? initial = null)
        {
            if (initial == null) return;

            foreach (URN urn in initial)
                if (!urn.IsNullOrEmpty())
                    wearables.Add(urn.Shorten());
        }

        public void ApplyTo(Profile profile)
        {
            if (wearables.Count == 0 || !TryGetBackingSet(profile, out HashSet<URN>? profileWearables)) return;

            foreach (URN wearable in wearables)
                profileWearables.Add(wearable);
        }

        /// <summary>Avatar.wearables is internal to DCL.SharedAPI, so the backing set is reached by casting the public read-only view; a change of backing type is reported rather than ignored.</summary>
        private static bool TryGetBackingSet(Profile profile, [NotNullWhen(true)] out HashSet<URN>? profileWearables)
        {
            if (profile.Avatar.Wearables is HashSet<URN> set)
            {
                profileWearables = set;
                return true;
            }

            ReportHub.LogError(ReportCategory.PROFILE, $"{nameof(ForcedWearables)} cannot reach the profile's wearable set: forced wearables are disabled");
            profileWearables = null;
            return false;
        }
    }
}
