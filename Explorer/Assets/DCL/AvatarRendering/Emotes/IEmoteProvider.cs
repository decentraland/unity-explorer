using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;

namespace DCL.AvatarRendering.Emotes
{
    public interface IEmoteProvider
    {
        UniTask<IReadOnlyList<IEmote>> GetOwnedEmotesAsync(string userId, CancellationToken ct);

        UniTask<IReadOnlyList<IEmote>> GetEmotesAsync(IEnumerable<URN> emoteIds, CancellationToken ct);
    }
}
