using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading;
using System.Collections.Generic;

namespace DCL.AvatarRendering.Emotes
{
    public interface IEmoteStorage : IAvatarElementStorage<IEmote, EmoteDTO>
    {
        List<URN> EmbededURNs { get; }
        void AddEmbeded(URN urn, IEmote emote);
    }
}
