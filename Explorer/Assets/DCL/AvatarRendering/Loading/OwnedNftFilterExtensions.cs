using System.Collections.Generic;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables.Components;

namespace DCL.AvatarRendering.Loading
{
    public static class OwnedNftFilterExtensions
    {
        /// <summary>
        ///     True when at least one owned instance isn't excluded by the filter (e.g. not pending a gift).
        ///     <paramref name="excludeInstance" />, if set, counts as unavailable even when the filter doesn't yet
        ///     know it — the gifted instance during the optimistic window before it's added to the pending set.
        /// </summary>
        public static bool HasAvailableInstance(this IOwnedNftFilter? filter,
            IReadOnlyDictionary<URN, NftBlockchainOperationEntry> registry,
            URN? excludeInstance = null)
        {
            foreach (NftBlockchainOperationEntry entry in registry.Values)
            {
                if (excludeInstance.HasValue && entry.Urn.Equals(excludeInstance.Value)) continue;
                if (filter == null || !filter.ShouldExclude(entry.Urn)) return true;
            }

            return false;
        }
    }
}
