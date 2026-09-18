using CommunicationData.URLHelpers;
using DCL.Diagnostics;
using System;
using System.Collections.Generic;

namespace DCL.Profiles.Self
{
    /// <summary>
    ///     Wearables faked as equipped on the self profile for the current session. They are applied to every
    ///     profile <see cref="SelfProfile" /> hands out — which is what the player entity, and therefore the
    ///     avatar, is built from — and removed again from anything about to be deployed, so they never reach the
    ///     catalyst even after the backpack has copied them into <c>IEquippedWearables</c>.
    ///     Lets a wearable be worn without owning it, e.g. to check a locally converted asset bundle in-world.
    /// </summary>
    public class ForcedWearables
    {
        private readonly HashSet<URN> wearables = new ();

        /// <summary>
        ///     Everything forced at any point this session. Stripping this rather than the current set matters
        ///     because un-forcing a wearable does not un-equip it: the backpack may already have copied it into
        ///     IEquippedWearables, from where it would otherwise be deployed.
        /// </summary>
        private readonly HashSet<URN> everForced = new ();

        /// <summary>Raised when the set changes, so the in-world avatar can be rebuilt.</summary>
        public event Action? Changed;

        public IReadOnlyCollection<URN> Wearables => wearables;

        public ForcedWearables(IEnumerable<URN>? initial = null)
        {
            if (initial == null) return;

            foreach (URN urn in initial)
                if (!urn.IsNullOrEmpty())
                {
                    URN shortened = urn.Shorten();
                    wearables.Add(shortened);
                    everForced.Add(shortened);
                }
        }

        public void Add(URN wearable)
        {
            if (wearable.IsNullOrEmpty()) return;

            URN shortened = wearable.Shorten();
            everForced.Add(shortened);

            if (wearables.Add(shortened))
                Changed?.Invoke();
        }

        public void Remove(URN wearable)
        {
            if (wearables.Remove(wearable.Shorten()))
                Changed?.Invoke();
        }

        public void Clear()
        {
            if (wearables.Count == 0) return;

            wearables.Clear();
            Changed?.Invoke();
        }

        public void ApplyTo(Profile profile)
        {
            if (wearables.Count == 0 || !TryGetBackingSet(profile, out HashSet<URN> profileWearables)) return;

            foreach (URN wearable in wearables)
                profileWearables.Add(wearable);
        }

        public void RemoveFrom(Profile profile)
        {
            if (everForced.Count == 0 || !TryGetBackingSet(profile, out HashSet<URN> profileWearables)) return;

            foreach (URN wearable in everForced)
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
