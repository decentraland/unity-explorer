using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading;
using System;
using System.Collections.Generic;
using DCL.AvatarRendering.Wearables.Components;

namespace DCL.AvatarRendering.Emotes
{
    public interface IEmoteStorage : IAvatarElementStorage<IEmote, EmoteDTO>
    {
        List<URN> EmbededURNs { get; }
        void AddEmbeded(URN urn, IEmote emote);
        bool TryGetLatestTransferredAt(URN nftUrn, out DateTime latestTransferredAt);
        bool TryGetLatestOwnedNft(URN nftUrn, out NftBlockchainOperationEntry entry);
        Dictionary<URN, Dictionary<URN, NftBlockchainOperationEntry>> AllOwnedNftRegistry { get; }
    }
}
