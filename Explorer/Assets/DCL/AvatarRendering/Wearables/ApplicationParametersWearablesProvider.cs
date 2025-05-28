using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
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
        private const string BUILDER_DTO_URL_COL_ID = "[COL-ID]";

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

        public async UniTask<(IReadOnlyList<IWearable> results, int totalAmount)> GetAsync(int pageSize, int pageNumber, CancellationToken ct,
            IWearablesProvider.SortingField sortingField = IWearablesProvider.SortingField.Date,
            IWearablesProvider.OrderBy orderBy = IWearablesProvider.OrderBy.Descending,
            string? category = null,
            IWearablesProvider.CollectionType collectionType = IWearablesProvider.CollectionType.All,
            string? name = null,
            List<IWearable>? results = null,
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

                    if (maleWearables != null)
                        resultWearablesBuffer.AddRange(maleWearables);

                    if (femaleWearables != null)
                        resultWearablesBuffer.AddRange(femaleWearables);

                    int pageIndex = pageNumber - 1;
                    results.AddRange(resultWearablesBuffer.Skip(pageIndex * pageSize).Take(pageSize));
                    return (results, resultWearablesBuffer.Count);
                }
            }

            if (appArgs.TryGetValue(AppArgsFlags.SELF_PREVIEW_BUILDER_COLLECTIONS, out string? collectionsCsv))
            {
                string[] collections = collectionsCsv!.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                                   .ToArray();

                results ??= new List<IWearable>();
                var localBuffer = ListPool<IWearable>.Get();
                for (var i = 0; i < collections.Length; i++)
                {
                    // localBuffer accumulates the loaded wearables
                    await source.GetAsync(pageSize, pageNumber, ct, sortingField, orderBy, category, collectionType, name, localBuffer,
                        loadingArguments: new CommonLoadingArguments(
                            builderDTOsUrl.Replace(BUILDER_DTO_URL_COL_ID, collections[i]),
                            cancellationTokenSource: new CancellationTokenSource()
                        ),
                        needsBuilderAPISigning: true);
                }

                int pageIndex = pageNumber - 1;
                results.AddRange(localBuffer.Skip(pageIndex * pageSize).Take(pageSize));

                int count = localBuffer.Count;
                ListPool<IWearable>.Release(localBuffer);

                return (results, count);
            }

            // Regular path without any "self-preview" element
            return await source.GetAsync(pageSize, pageNumber, ct, sortingField, orderBy, category, collectionType, name, results);
        }

        public async UniTask<IReadOnlyCollection<IWearable>?> RequestPointersAsync(IReadOnlyCollection<URN> pointers,
            BodyShape bodyShape, CancellationToken ct)
            => await source.RequestPointersAsync(pointers, bodyShape, ct);
    }
}
