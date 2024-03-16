using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Web3;
using System.Collections.Generic;
using System.Threading;

namespace DCL.AvatarRendering.Emotes
{
    public interface IEmoteProvider
    {
        UniTask<IReadOnlyList<IEmote>> GetOwnedEmotesAsync(Web3Address userId, CancellationToken ct, int? pageNum = null, int? pageSize = null, URN? collectionId = null);

        UniTask<IReadOnlyList<IEmote>> GetEmotesAsync(IEnumerable<URN> emoteIds, CancellationToken ct);
    }
}
