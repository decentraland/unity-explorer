using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading;
using System;
using System.Collections.Generic;
using DCL.AvatarRendering.Wearables.Components;

namespace DCL.AvatarRendering.Emotes
{
    public interface IEmoteStorage : IAvatarElementStorage<IEmote, EmoteDTO>
    {
        IReadOnlyList<URN> BaseEmotesUrns { get; }
        bool TryGetLatestTransferredAt(URN nftUrn, out DateTime latestTransferredAt);
        bool TryGetLatestOwnedNft(URN nftUrn, out NftBlockchainOperationEntry entry);
        IReadOnlyDictionary<URN, Dictionary<URN, NftBlockchainOperationEntry>> AllOwnedNftRegistry { get; }
        void SetBaseEmotesUrns(IReadOnlyCollection<URN> urns);
    }
}
