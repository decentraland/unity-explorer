using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;
using System.Threading;

namespace DCL.AvatarRendering.Loading
{
    public interface IElementsProvider<TTrimmedElement, TElement, in TParams>
    {
        UniTask<(IReadOnlyList<TTrimmedElement> results, int totalAmount)> GetTrimmedByParamsAsync(
            TParams parameters,
            CancellationToken ct,
            List<TTrimmedElement>? results = null,
            CommonLoadingArguments? loadingArguments = null,
            bool needsBuilderAPISigning = false
            );

        UniTask<IReadOnlyCollection<TElement>?> GetByPointersAsync(
            IReadOnlyCollection<URN> pointers,
            BodyShape bodyShape,
            CancellationToken ct,
            List<TElement>? results = null);
    }

    public static class IElementsProviderQueryParams
    {
        public const string PAGE_NUMBER = "pageNum";
        public const string PAGE_SIZE = "pageSize";
        public const string TRIMMED = "trimmed";
        public const string INCLUDE_AMOUNT = "includeAmount";
        public const string ORDER_BY = "orderBy";
        public const string ORDER_DIRECTION = "direction";
        public const string NAME = "name";

        public static class Values
        {
            public const string ASCENDING = "asc";
            public const string DESCENDING = "desc";
        }
    }

}
