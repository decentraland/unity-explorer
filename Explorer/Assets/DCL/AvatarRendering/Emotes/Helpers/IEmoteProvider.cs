using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
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

        public readonly struct OwnedEmotesRequestOptions
        {
            public readonly int? pageNum;
            public readonly int? pageSize;
            public readonly URN? collectionId;
            public readonly OrderOperation? orderOperation;
            public readonly string? name;

            public OwnedEmotesRequestOptions(int? pageNum, int? pageSize, URN? collectionId, IEmoteProvider.OrderOperation? orderOperation, string? name)
            {
                this.pageNum = pageNum;
                this.pageSize = pageSize;
                this.collectionId = collectionId;
                this.orderOperation = orderOperation;
                this.name = name;
            }
        }

        /// <returns>Total amount</returns>
        UniTask<int> GetOwnedEmotesAsync(
            Web3Address userId,
            CancellationToken ct,
            OwnedEmotesRequestOptions requestOptions,
            List<IEmote> output
        );

        UniTask GetEmotesAsync(
            IReadOnlyCollection<URN> emoteIds,
            BodyShape bodyShape,
            CancellationToken ct,
            List<IEmote> output
        );
    }
}
