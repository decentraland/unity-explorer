using CommunicationData.URLHelpers;
using DCL.Diagnostics;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace DCL.Profiles.Self
{
    /// <summary>
    ///     Session-scoped set of wearable URNs faked as equipped on the self profile.
    ///     <see cref="ApplyTo" /> injects the set into every profile <see cref="SelfProfile" /> hands out, which is
    ///     what the player entity — and therefore the avatar — is built from.
    ///     Lets a wearable be worn without owning it, e.g. to check a locally converted asset bundle in-world.
    ///     While <see cref="Any" /> is true the session never deploys a profile, so the faked set cannot reach the
    ///     catalyst by any route.
    ///     Fixed at construction: the set comes from the launch arguments or the debug settings.
    /// </summary>
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

        /// <summary>
        ///     Avatar.wearables is internal to DCL.SharedAPI (the SharedAPI/ folder carries an .asmref into it), so
        ///     the backing set is reached through the public read-only view. Reported rather than ignored: a change
        ///     of backing type would otherwise leave the avatar without the forced wearables and no hint as to why.
        /// </summary>
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
