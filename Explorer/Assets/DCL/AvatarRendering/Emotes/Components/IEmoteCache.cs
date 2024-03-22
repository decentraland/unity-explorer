using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Emotes.Components;
using DCL.AvatarRendering.Wearables.Components;
using DCL.Optimization.PerformanceBudgeting;
using System.Collections.Generic;

namespace DCL.AvatarRendering.Emotes
{
    public interface IEmoteCache
    {
        bool TryGetEmote(URN urn, out IEmote emote);

        void Set(URN urn, IEmote emote);

        IEmote GetOrAddEmoteByDTO(EmoteDTO emoteDto, bool qualifiedForUnloading = true);

        void Unload(IPerformanceBudget frameTimeBudget);

        void SetOwnedNft(URN urn, NftBlockchainOperationEntry operation);

        bool TryGetOwnedNftRegistry(URN nftUrn, out IReadOnlyDictionary<URN, NftBlockchainOperationEntry> registry);
    }
}
