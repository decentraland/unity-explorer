using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables;
using DCL.Web3;
using System.Collections.Generic;
using System.Threading;

namespace DCL.AvatarRendering.Emotes
{
    public interface IEmoteProvider
    {
        public readonly struct OrderOperation
        {
            public readonly string By;
            public readonly bool IsAscendent;

            public OrderOperation(string by, bool isAscendent)
            {
                By = by;
                IsAscendent = isAscendent;
            }
        }

        UniTask<(IReadOnlyList<IEmote> emotes, int totalAmount)> GetOwnedEmotesAsync(Web3Address userId, CancellationToken ct,
            int? pageNum = null, int? pageSize = null, URN? collectionId = null,
            OrderOperation? orderOperation = null, string? name = null);

        UniTask<IReadOnlyList<IEmote>> GetEmotesAsync(IReadOnlyCollection<URN> emoteIds, BodyShape bodyShape, CancellationToken ct);
    }
}
