using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables.Components;
using DCL.Optimization.PerformanceBudgeting;
using System.Collections.Generic;

namespace DCL.AvatarRendering.Loading
{
    public interface IAvatarElementCache<TElement, in TDTO>
    {
        bool TryGetElement(URN urn, out TElement emote);

        void Set(URN urn, TElement emote);

        TElement GetOrAddByDTO(TDTO dto, bool qualifiedForUnloading = true);

        void Unload(IPerformanceBudget frameTimeBudget);

        void SetOwnedNft(URN urn, NftBlockchainOperationEntry operation);

        bool TryGetOwnedNftRegistry(URN nftUrn, out IReadOnlyDictionary<URN, NftBlockchainOperationEntry> registry);
    }
}
