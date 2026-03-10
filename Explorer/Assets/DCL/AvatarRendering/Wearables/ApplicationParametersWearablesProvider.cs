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
        private readonly List<ITrimmedWearable> resultWearablesBuffer = new ();
        private readonly string builderDTOsUrl;

        public ApplicationParametersWearablesProvider(IAppArgs appArgs, IWearablesProvider source, string builderDTOsUrl)
        {
            this.appArgs = appArgs;
            this.source = source;
            this.builderDTOsUrl = builderDTOsUrl;
        }

        public async UniTask<(IReadOnlyList<ITrimmedWearable> results, int totalAmount)> GetTrimmedByParamsAsync(
            IWearablesProvider.Params parameters,
            CancellationToken ct,
            List<ITrimmedWearable>? results = null,
            CommonLoadingArguments? loadingArguments = null,
            bool needsBuilderAPISigning = false)
        {
            if (appArgs.TryGetValue(AppArgsFlags.SELF_PREVIEW_WEARABLES, out string? wearablesCsv))
            {
                URN[] pointers = wearablesCsv!.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => new URN(s))
                    .ToArray();

                (IReadOnlyCollection<IWearable>? maleWearables, IReadOnlyCollection<IWearable>? femaleWearables) =
                    await UniTask.WhenAll(GetByPointersAsync(pointers, BodyShape.MALE, ct),
                        GetByPointersAsync(pointers, BodyShape.FEMALE, ct));

                results ??= new List<ITrimmedWearable>();

                lock (resultWearablesBuffer)
                {
                    resultWearablesBuffer.Clear();
                    if (maleWearables != null)
                        resultWearablesBuffer.AddRange(maleWearables);

                    if (femaleWearables != null)
                        resultWearablesBuffer.AddRange(femaleWearables);

                    int pageIndex = parameters.PageNumber - 1;
                    results.AddRange(resultWearablesBuffer.Skip(pageIndex * parameters.PageSize).Take(parameters.PageSize));
                    return (results, resultWearablesBuffer.Count);
                }
            }

            if (appArgs.TryGetValue(AppArgsFlags.SELF_PREVIEW_BUILDER_COLLECTIONS, out string? collectionsCsv))
            {
                string[] collections = collectionsCsv!.Split(',', StringSplitOptions.RemoveEmptyEntries);

                results ??= new List<ITrimmedWearable>();
                var localBuffer = ListPool<ITrimmedWearable>.Get();
                for (var i = 0; i < collections.Length; i++)
                {
                    // localBuffer accumulates the loaded wearables
                    await source.GetTrimmedByParamsAsync(
                        parameters,
                        ct,
                        localBuffer,
                        loadingArguments: new CommonLoadingArguments(
                            builderDTOsUrl.Replace(LoadingConstants.BUILDER_DTO_URL_COL_ID_PLACEHOLDER, collections[i]),
                            cancellationTokenSource: new CancellationTokenSource()
                        ),
                        needsBuilderAPISigning: true
                    );
                }

                // Include ALL user's available wearables (loop pages)
                // Higher page size to do a lot less requests.
                const int OWNED_PAGE_SIZE = 200;
                IWearablesProvider.Params subParameters = parameters;
                subParameters.PageSize = OWNED_PAGE_SIZE;
                subParameters.PageNumber = 1;
                int ownedTotal = int.MaxValue;
                using var ownedPageBufferScope = ListPool<ITrimmedWearable>.Get(out var ownedPageBuffer);

                while (localBuffer.Count < ownedTotal)
                {
                    ownedPageBuffer.Clear();
                    (var ownedPageResults, int ownedPageTotal) = await source.GetTrimmedByParamsAsync(
                        subParameters,
                        ct,
                        results: ownedPageBuffer
                    );

                    ownedTotal = ownedPageTotal;

                    if (ownedPageResults.Count == 0)
                        break;

                    localBuffer.AddRange(ownedPageResults);
                    subParameters.PageNumber++;
                }

                // De-duplicate by URN and paginate the unified list
                var unified = localBuffer
                    .GroupBy(w => w.GetUrn())
                    .Select(g => g.First())
                    .ToList();

                int pageIndex = parameters.PageNumber - 1;
                results.AddRange(unified.Skip(pageIndex * parameters.PageSize).Take(parameters.PageSize));

                int count = unified.Count;

                ListPool<ITrimmedWearable>.Release(localBuffer);

                return (results, count);
            }

            return await source.GetTrimmedByParamsAsync(
                parameters,
                ct,
                results: results,
                loadingArguments: loadingArguments,
                needsBuilderAPISigning: needsBuilderAPISigning
            );
        }

        public async UniTask<IReadOnlyCollection<IWearable>?> GetByPointersAsync(IReadOnlyCollection<URN> pointers,
            BodyShape bodyShape, CancellationToken ct, List<IWearable>? results = null)
            => await source.GetByPointersAsync(pointers, bodyShape, ct, results);
    }
}
