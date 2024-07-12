using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Emotes.OwnedNfts;
using DCL.AvatarRendering.Wearables.Components;
using System.Collections.Generic;

namespace DCL.AvatarRendering.Emotes.OwnedNftHub
{
    public class OwnedNftHub : IOwnedNftHub
    {
        private readonly Dictionary<URN, Dictionary<URN, NftBlockchainOperationEntry>> ownedNftsRegistry =
            new (new Dictionary<URN, Dictionary<URN, NftBlockchainOperationEntry>>(), URNIgnoreCaseEqualityComparer.Default);

        public void SetOwnedNft(URN nftUrn, NftBlockchainOperationEntry entry)
        {
            if (!ownedNftsRegistry.TryGetValue(nftUrn, out Dictionary<URN, NftBlockchainOperationEntry> ownedWearableRegistry))
            {
                ownedWearableRegistry = new Dictionary<URN, NftBlockchainOperationEntry>(new Dictionary<URN, NftBlockchainOperationEntry>(),
                    URNIgnoreCaseEqualityComparer.Default);

                ownedNftsRegistry[nftUrn] = ownedWearableRegistry;
            }

            ownedWearableRegistry![entry.Urn] = entry;
        }

        public bool TryGetOwnedNftRegistry(URN nftUrn, out IReadOnlyDictionary<URN, NftBlockchainOperationEntry>? registry)
        {
            bool result = ownedNftsRegistry.TryGetValue(nftUrn, out Dictionary<URN, NftBlockchainOperationEntry> r);
            registry = r;
            return result;
        }
    }
}
