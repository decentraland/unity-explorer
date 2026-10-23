using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading;
using DCL.AvatarRendering.Wearables.Components;
using System.Collections.Generic;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    public static class OwnedNftFilterExtensions
    {
        /// <summary>
        ///     True when the user owns <paramref name="itemUrn" /> but every owned instance is excluded by the filter, e.g. all of them are pending a gift.
        /// </summary>
        public static bool IsFullyPending(this IOwnedNftFilter filter, IWearableStorage storage, URN itemUrn) =>
            storage.TryGetOwnedNftRegistry(itemUrn, out IReadOnlyDictionary<URN, NftBlockchainOperationEntry> registry)
            && registry.Count > 0
            && !filter.HasAvailableInstance(registry);
    }
}
