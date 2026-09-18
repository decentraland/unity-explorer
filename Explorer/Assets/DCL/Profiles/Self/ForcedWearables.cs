using CommunicationData.URLHelpers;
using DCL.Diagnostics;
using System.Collections.Generic;

namespace DCL.Profiles.Self
{
    /// <summary>
    ///     Wearables faked as equipped on the self profile for the current session. They are applied to every
    ///     profile <see cref="SelfProfile" /> hands out — which is what the player entity, and therefore the
    ///     avatar, is built from — and removed again from anything about to be deployed, so they never reach the
    ///     catalyst.
    ///     Lets a wearable be worn without owning it, e.g. to check a locally converted asset bundle in-world.
    ///     Fixed at construction: the set comes from the launch arguments or the debug settings.
    /// </summary>
    public class ForcedWearables
    {
        private readonly HashSet<URN> wearables = new ();

        public ForcedWearables(IEnumerable<URN>? initial = null)
        {
            if (initial == null) return;

            foreach (URN urn in initial)
                if (!urn.IsNullOrEmpty())
                    wearables.Add(urn.Shorten());
        }

        public void ApplyTo(Profile profile)
        {
            if (wearables.Count == 0 || !TryGetBackingSet(profile, out HashSet<URN> profileWearables)) return;

            foreach (URN wearable in wearables)
                profileWearables.Add(wearable);
        }

        public void RemoveFrom(Profile profile)
        {
            if (wearables.Count == 0 || !TryGetBackingSet(profile, out HashSet<URN> profileWearables)) return;

            foreach (URN wearable in wearables)
                profileWearables.Remove(wearable);
        }

        /// <summary>
        ///     Avatar.wearables is internal to DCL.SharedAPI (the SharedAPI/ folder carries an .asmref into it), so
        ///     the backing set is reached through the public read-only view. Reported rather than ignored: a change
        ///     of backing type would otherwise make both apply and strip silently do nothing, and a strip that does
        ///     nothing is what would let a faked wearable be deployed.
        /// </summary>
        private static bool TryGetBackingSet(Profile profile, out HashSet<URN> profileWearables)
        {
            if (profile.Avatar.Wearables is HashSet<URN> set)
            {
                profileWearables = set;
                return true;
            }

            ReportHub.LogError(ReportCategory.PROFILE, $"{nameof(ForcedWearables)} cannot reach the profile's wearable set: forced wearables are disabled");
            profileWearables = null!;
            return false;
        }
    }
}
