using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.DTO;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
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

        private void ConvertAndAdd(List<ITrimmedWearable> destination, IReadOnlyCollection<IWearable> sourceCollection)
        {
            foreach (var wearable in sourceCollection)
                destination.Add(wearable.Convert());
        }

        public async UniTask<(IReadOnlyList<ITrimmedWearable> results, int totalAmount)> GetAsync(int pageSize, int pageNumber, CancellationToken ct,
            IWearablesProvider.SortingField sortingField = IWearablesProvider.SortingField.Date,
            IWearablesProvider.OrderBy orderBy = IWearablesProvider.OrderBy.Descending,
            string? category = null,
            IWearablesProvider.CollectionType collectionType = IWearablesProvider.CollectionType.All,
            string? name = null,
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
                    await UniTask.WhenAll(RequestPointersAsync(pointers, BodyShape.MALE, ct),
                        RequestPointersAsync(pointers, BodyShape.FEMALE, ct));

                results ??= new List<ITrimmedWearable>();

                lock (resultWearablesBuffer)
                {
                    resultWearablesBuffer.Clear();

                    if (maleWearables != null)
                        ConvertAndAdd(resultWearablesBuffer, maleWearables);

                    if (femaleWearables != null)
                        ConvertAndAdd(resultWearablesBuffer, femaleWearables);

                    int pageIndex = pageNumber - 1;
                    results.AddRange(resultWearablesBuffer.Skip(pageIndex * pageSize).Take(pageSize));
                    return (results, resultWearablesBuffer.Count);
                }
            }

            if (appArgs.TryGetValue(AppArgsFlags.SELF_PREVIEW_BUILDER_COLLECTIONS, out string? collectionsCsv))
            {
                string[] collections = collectionsCsv!.Split(',', StringSplitOptions.RemoveEmptyEntries).ToArray();

                results ??= new List<ITrimmedWearable>();
                var localBuffer = ListPool<ITrimmedWearable>.Get();
                for (var i = 0; i < collections.Length; i++)
                {
                    // localBuffer accumulates the loaded wearables
                    await source.GetAsync(pageSize, pageNumber, ct, sortingField, orderBy, category, collectionType, name, localBuffer,
                        loadingArguments: new CommonLoadingArguments(
                            builderDTOsUrl.Replace(LoadingConstants.BUILDER_DTO_URL_COL_ID_PLACEHOLDER, collections[i]),
                            cancellationTokenSource: new CancellationTokenSource()
                        ),
                        needsBuilderAPISigning: true);
                }

                // Include ALL user's available wearables (loop pages)
                // Higher page size to do a lot less requests.
                const int OWNED_PAGE_SIZE = 200;
                int ownedPage = 1;
                int ownedTotal = int.MaxValue;
                using var ownedPageBufferScope = ListPool<ITrimmedWearable>.Get(out var ownedPageBuffer);

                while (localBuffer.Count < ownedTotal)
                {
                    ownedPageBuffer.Clear();
                    (IReadOnlyList<ITrimmedWearable> ownedPageResults, int ownedPageTotal) = await source.GetAsync(
                        OWNED_PAGE_SIZE,
                        ownedPage,
                        ct,
                        sortingField,
                        orderBy,
                        category,
                        collectionType,
                        name,
                        ownedPageBuffer
                    );

                    ownedTotal = ownedPageTotal;

                    if (ownedPageResults.Count == 0)
                        break;

                    localBuffer.AddRange(ownedPageResults);
                    ownedPage++;
                }

                // De-duplicate by URN and paginate the unified list
                var unified = localBuffer
                    .GroupBy(w => w.GetUrn())
                    .Select(g => g.First())
                    .ToList();

                int pageIndex = pageNumber - 1;
                results.AddRange(unified.Skip(pageIndex * pageSize).Take(pageSize));

                int count = unified.Count;

                ListPool<ITrimmedWearable>.Release(localBuffer);

                return (results, count);
            }

            // Regular path without any "self-preview" element
            return await source.GetAsync(pageSize, pageNumber, ct, sortingField, orderBy, category, collectionType, name, results);
        }

        public async UniTask<IReadOnlyCollection<IWearable>?> RequestPointersAsync(IReadOnlyCollection<URN> pointers,
            BodyShape bodyShape, CancellationToken ct)
            => await source.RequestPointersAsync(pointers, bodyShape, ct);
    }

    public static class WearableExtensions
    {
        public static ITrimmedWearable Convert(this IWearable wearable)
        {
            var result = new TrimmedWearable
                {
                    ThumbnailAssetResult = wearable.ThumbnailAssetResult,
                    Model = wearable.Model.Convert()
                };

            return result;
        }

        public static StreamableLoadingResult<TrimmedWearableDTO> Convert(this StreamableLoadingResult<WearableDTO> wearableModel)
        {
            if (!wearableModel.Succeeded)
                return new StreamableLoadingResult<TrimmedWearableDTO>(wearableModel.ReportData, wearableModel.Exception!);

            var dto = wearableModel.Asset!;

            return new StreamableLoadingResult<TrimmedWearableDTO>(dto.Convert());
        }

        public static TrimmedWearableDTO Convert(this WearableDTO wearableDTO)
        {
            var trimmedDTO = new TrimmedWearableDTO
            {
                id = wearableDTO.id,
                thumbnail = wearableDTO.metadata.thumbnail, //???
                metadata = new TrimmedWearableDTO.WearableMetadataDto
                {
                    id = wearableDTO.metadata.id,
                    rarity = wearableDTO.metadata.rarity,
                    data = new TrimmedWearableDTO.WearableMetadataDto.DataDto
                    {
                        category = wearableDTO.metadata.data.category,
                        representations = wearableDTO.metadata.data.representations
                    }
                }
            };

            return trimmedDTO;
        }
    }
}
