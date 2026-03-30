using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading;

namespace DCL.AvatarRendering.Emotes
{
    public interface IEmoteProvider : IElementsProvider<ITrimmedEmote, IEmote, IEmoteProvider.OwnedEmotesRequestOptions>
    {
        public readonly struct OrderOperation
        {
            public readonly string By;
            public readonly bool IsAscending;

            public OrderOperation(string by, bool isAscending)
            {
                By = by;
                IsAscending = isAscending;
            }
        }

        public readonly struct OwnedEmotesRequestOptions
        {
            public readonly int? PageNum;
            public readonly int? PageSize;
            public readonly URN? CollectionId;
            public readonly OrderOperation? OrderOperation;
            public readonly string? Name;
            public readonly bool? IncludeAmount;

            public OwnedEmotesRequestOptions(int? pageNum,
                int? pageSize,
                URN? collectionId,
                OrderOperation? orderOperation,
                string? name,
                bool? includeAmount = null)
            {
                this.PageNum = pageNum;
                this.PageSize = pageSize;
                this.CollectionId = collectionId;
                this.OrderOperation = orderOperation;
                this.Name = name;
                this.IncludeAmount = includeAmount;
            }
        }
    }
}
