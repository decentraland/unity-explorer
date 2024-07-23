using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables.Components;
using DCL.Optimization.PerformanceBudgeting;
using System.Collections.Generic;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    /// <summary>
    ///     Defines the functionalities for wearable catalog. Works like cache by storing instances of <see cref="IWearable" /> by string keys.
    /// </summary>
    public interface IWearableCatalog
    {
        /// <summary>
        ///     Retrieves a wearable by its DTO or adds a new one if it doesn't exist.
        /// </summary>
        /// <param name="wearableDto">The wearable DTO</param>
        /// <param name="qualifiedForUnloading">Determines if the wearable should be unloaded when memory is full</param>
        /// <returns>An instance of the <see cref="IWearable" /> type.</returns>
        IWearable GetOrAddWearableByDTO(WearableDTO wearableDto, bool qualifiedForUnloading = true);

        /// <summary>
        ///     Adds an empty wearable to the catalog.
        /// </summary>
        /// <param name="urn">The loading intention pointer.</param>
        /// <param name="qualifiedForUnloading">Determines if the wearable should be unloaded when memory is full</param>
        void AddEmptyWearable(URN urn, bool qualifiedForUnloading = true);

        /// <summary>
        ///     Attempts to retrieve a wearable from the catalog.
        /// </summary>
        /// <param name="wearableURN">The wearable URN identifier.</param>
        /// <param name="wearable">The wearable instance if found.</param>
        /// <returns>Returns true if the wearable exists; otherwise, false.</returns>
        bool TryGetWearable(URN wearableURN, out IWearable wearable);

        /// <summary>
        ///     Retrieves default wearable from the catalog.
        /// </summary>
        /// <param name="bodyShape">The body shape.</param>
        /// <param name="category">The category.</param>
        /// <returns>An instance of the <see cref="IWearable" /> type.</returns>
        IWearable GetDefaultWearable(BodyShape bodyShape, string category);

        /// <summary>
        ///     Unloads the wearable from the catalog by a frame time budget provider.
        /// </summary>
        /// <param name="frameTimeBudget">The frame time budget provider.</param>
        void Unload(IPerformanceBudget frameTimeBudget);

        void SetOwnedNft(URN nftUrn, NftBlockchainOperationEntry entry);

        bool TryGetOwnedNftRegistry(URN nftUrn, out IReadOnlyDictionary<URN, NftBlockchainOperationEntry> registry);
    }
}
