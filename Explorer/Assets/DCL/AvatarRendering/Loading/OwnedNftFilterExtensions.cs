using System.Collections.Generic;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables.Components;

namespace DCL.AvatarRendering.Loading
{
    public static class OwnedNftFilterExtensions
    {
        /// <summary>
        ///     Returns true when the user still holds at least one usable owned instance of an item, i.e. a
        ///     registry entry that the filter does not exclude (for example, not pending a gift transfer).
        ///     <paramref name="excludeInstance" />, when provided, is treated as unavailable even if the filter
        ///     does not know about it yet — this is the gifted instance during the optimistic transfer window,
        ///     before it has been added to the pending set.
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
