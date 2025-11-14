using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using ECS.StreamableLoading.Common.Components;
using Global.AppArgs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine.Pool;

namespace DCL.AvatarRendering.Wearables
{
    public class ApplicationParametersWearablesProvider : IWearablesProvider
    {
        private readonly IAppArgs appArgs;
        private readonly IWearablesProvider source;
        private readonly List<IWearable> resultWearablesBuffer = new ();
        private readonly string builderDTOsUrl;

        public ApplicationParametersWearablesProvider(IAppArgs appArgs, IWearablesProvider source, string builderDTOsUrl)
        {
            this.appArgs = appArgs;
            this.source = source;
            this.builderDTOsUrl = builderDTOsUrl;
        }

        public async UniTask<(IReadOnlyList<IWearable> results, int totalAmount)> GetAsync(
            int pageSize, int pageNumber, CancellationToken ct,
            IWearablesProvider.SortingField sortingField = IWearablesProvider.SortingField.Date,
            IWearablesProvider.OrderBy orderBy = IWearablesProvider.OrderBy.Descending,
            string? category = null,
            IWearablesProvider.CollectionType collectionType = IWearablesProvider.CollectionType.All,
            string? name = null,
            List<IWearable>? results = null,
            string? network = null,
            bool? includeAmount = null,
            CommonLoadingArguments? loadingArguments = null,
            bool needsBuilderAPISigning = false)
        {
            if (appArgs.TryGetValue(AppArgsFlags.SELF_PREVIEW_WEARABLES, out string? wearablesCsv))
            {
                URN[] pointers = wearablesCsv!.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => new URN(s))
                    .ToArray();

                (IReadOnlyCollection<IWearable>? maleWearables, IReadOnlyCollection<IWearable>? femaleWearables) =
                    await UniTask.WhenAll(RequestPointersAsync(pointers, BodyShape.MALE, ct),
                        RequestPointersAsync(pointers, BodyShape.FEMALE, ct));

                results ??= new List<IWearable>();

                lock (resultWearablesBuffer)
                {
                    resultWearablesBuffer.Clear();
                    if (maleWearables != null) resultWearablesBuffer.AddRange(maleWearables);
                    if (femaleWearables != null) resultWearablesBuffer.AddRange(femaleWearables);
                    int pageIndex = pageNumber - 1;
                    results.AddRange(resultWearablesBuffer.Skip(pageIndex * pageSize).Take(pageSize));
                    return (results, resultWearablesBuffer.Count);
                }
            }

            if (appArgs.TryGetValue(AppArgsFlags.SELF_PREVIEW_BUILDER_COLLECTIONS, out string? collectionsCsv))
            {
                string[] collections = collectionsCsv!.Split(',', StringSplitOptions.RemoveEmptyEntries).ToArray();
                results ??= new List<IWearable>();
                var localBuffer = ListPool<IWearable>.Get();

                for (var i = 0; i < collections.Length; i++)
                {
                    await source.GetAsync(
                        pageSize: pageSize, pageNumber: pageNumber, ct: ct,
                        sortingField: sortingField, orderBy: orderBy, category: category, collectionType: collectionType, name: name,
                        results: localBuffer,
                        network: network,
                        includeAmount: includeAmount,
                        loadingArguments: new CommonLoadingArguments(
                            builderDTOsUrl.Replace(LoadingConstants.BUILDER_DTO_URL_COL_ID_PLACEHOLDER, collections[i]),
                            cancellationTokenSource: new CancellationTokenSource()
                        ),
                        needsBuilderAPISigning: true
                    );
                }

                const int OWNED_PAGE_SIZE = 200;
                int ownedPage = 1;
                int ownedTotal = int.MaxValue;
                using var ownedPageBufferScope = ListPool<IWearable>.Get(out var ownedPageBuffer);

                while (localBuffer.Count < ownedTotal)
                {
                    ownedPageBuffer.Clear();
                    
                    (IReadOnlyList<IWearable> ownedPageResults, int ownedPageTotal) = await source.GetAsync(
                        pageSize: OWNED_PAGE_SIZE, pageNumber: ownedPage, ct: ct,
                        sortingField: sortingField, orderBy: orderBy, category: category, collectionType: collectionType, name: name,
                        results: ownedPageBuffer,
                        network: network,
                        includeAmount: includeAmount
                    );

                    ownedTotal = ownedPageTotal;
                    if (ownedPageResults.Count == 0) break;
                    localBuffer.AddRange(ownedPageResults);
                    ownedPage++;
                }

                var unified = localBuffer.GroupBy(w => w.GetUrn()).Select(g => g.First()).ToList();
                int pageIndex = pageNumber - 1;
                results.AddRange(unified.Skip(pageIndex * pageSize).Take(pageSize));
                int count = unified.Count;
                ListPool<IWearable>.Release(localBuffer);
                return (results, count);
            }

            return await source.GetAsync(
                pageSize: pageSize, pageNumber: pageNumber, ct: ct,
                sortingField: sortingField, orderBy: orderBy, category: category, collectionType: collectionType, name: name,
                results: results,
                network: network,
                includeAmount: includeAmount,
                loadingArguments: loadingArguments,
                needsBuilderAPISigning: needsBuilderAPISigning
            );
        }

        public async UniTask<IReadOnlyCollection<IWearable>?> RequestPointersAsync(IReadOnlyCollection<URN> pointers,
            BodyShape bodyShape, CancellationToken ct)
            => await source.RequestPointersAsync(pointers, bodyShape, ct);
    }
}
