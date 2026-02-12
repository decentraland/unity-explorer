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

}
