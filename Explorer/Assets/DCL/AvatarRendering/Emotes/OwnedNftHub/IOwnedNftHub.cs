using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables.Components;
using System.Collections.Generic;

namespace DCL.AvatarRendering.Emotes.OwnedNfts
{
    public interface IOwnedNftHub
    {
        bool TryGetOwnedNftRegistry(URN nftUrn, out IReadOnlyDictionary<URN, NftBlockchainOperationEntry>? registry);

        void SetOwnedNft(URN urn, NftBlockchainOperationEntry operation);
    }
}
