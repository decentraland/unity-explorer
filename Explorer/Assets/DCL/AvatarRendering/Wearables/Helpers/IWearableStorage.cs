using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading;
using DCL.AvatarRendering.Wearables.Components;
using System;
using System.Collections.Generic;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    /// <summary>
    ///     Defines the functionalities for wearable catalog. Works like cache by storing instances of <see cref="IWearable" /> by string keys.
    /// </summary>
    public interface IWearableStorage : IAvatarElementStorage<IWearable, WearableDTO>
    {
        bool TryGetLatestTransferredAt(URN nftUrn, out DateTime latestTransferredAt);
        bool TryGetLatestOwnedNft(URN nftUrn, out NftBlockchainOperationEntry entry);
        IReadOnlyDictionary<URN, Dictionary<URN, NftBlockchainOperationEntry>> AllOwnedNftRegistry { get; }
    }
}
