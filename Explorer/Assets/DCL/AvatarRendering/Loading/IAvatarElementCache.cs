using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables.Components;
using DCL.Optimization.PerformanceBudgeting;
using System.Collections.Generic;

namespace DCL.AvatarRendering.Loading
{
    // TODO thread safe
    public interface IAvatarElementCache<TElement, in TDTO>
    {
        /// <summary>
        ///     Attempts to retrieve an element from the catalog.
        /// </summary>
        /// <param name="urn">The URN identifier.</param>
        /// <param name="element">The element instance if found.</param>
        /// <returns>Returns true if the element exists; otherwise, false.</returns>
        bool TryGetElement(URN urn, out TElement element);

        void Set(URN urn, TElement element);

        /// <summary>
        ///     Retrieves an element by its DTO or adds a new one if it doesn't exist.
        /// </summary>
        /// <param name="dto">The wearable DTO</param>
        /// <param name="qualifiedForUnloading">Determines if the wearable should be unloaded when memory is full</param>
        /// <returns>An instance of the <see cref="TElement" /> type.</returns>
        TElement GetOrAddByDTO(TDTO dto, bool qualifiedForUnloading = true);

        /// <summary>
        ///     Unloads the wearable from the catalog by a frame time budget provider.
        /// </summary>
        /// <param name="frameTimeBudget">The frame time budget provider.</param>
        void Unload(IPerformanceBudget frameTimeBudget);

        void SetOwnedNft(URN urn, NftBlockchainOperationEntry operation);

        bool TryGetOwnedNftRegistry(URN nftUrn, out IReadOnlyDictionary<URN, NftBlockchainOperationEntry> registry);
    }
}
