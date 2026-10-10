using System.Collections.Generic;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables.Components;

namespace DCL.AvatarRendering.Loading
{
    /// <summary>
    ///     Suppresses NFTs that should no longer count as owned (e.g. a transfer initiated locally but not yet
    ///     indexed). Implementations must be thread-safe.
    /// </summary>
    public interface IOwnedNftFilter
    {
        public bool ShouldExclude(URN fullUrn);

        /// <summary>
        ///     True when at least one owned instance isn't excluded by the filter (e.g. not pending a gift).
        ///     <paramref name="excludeInstance" />, if set, counts as unavailable even when the filter doesn't yet
        ///     know it — the gifted instance during the optimistic window before it's added to the pending set.
        /// </summary>
        public bool HasAvailableInstance(IReadOnlyDictionary<URN, NftBlockchainOperationEntry> registry, URN? excludeInstance = null)
        {
            foreach (NftBlockchainOperationEntry entry in registry.Values)
            {
                if (excludeInstance.HasValue && entry.Urn.Equals(excludeInstance.Value)) continue;
                if (!ShouldExclude(entry.Urn)) return true;
            }

            return false;
        }
    }
}
