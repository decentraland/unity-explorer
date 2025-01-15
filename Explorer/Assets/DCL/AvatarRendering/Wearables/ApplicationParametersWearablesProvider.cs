using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using Global.AppArgs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace DCL.AvatarRendering.Wearables
{
    public class ApplicationParametersWearablesProvider : IWearablesProvider
    {
        private readonly IAppArgs appArgs;
        private readonly IWearablesProvider source;
        private readonly List<IWearable> resultWearablesBuffer = new ();

        public ApplicationParametersWearablesProvider(IAppArgs appArgs,
            IWearablesProvider source)
        {
            this.appArgs = appArgs;
            this.source = source;
        }

        public async UniTask<(IReadOnlyList<IWearable> results, int totalAmount)> GetAsync(int pageSize, int pageNumber, CancellationToken ct,
            IWearablesProvider.SortingField sortingField = IWearablesProvider.SortingField.Date,
            IWearablesProvider.OrderBy orderBy = IWearablesProvider.OrderBy.Descending,
            string? category = null,
            IWearablesProvider.CollectionType collectionType = IWearablesProvider.CollectionType.All,
            string? name = null,
            List<IWearable>? results = null,
            string? intentionUrl = null)
        {
            if (!appArgs.TryGetValue(AppArgsFlags.SELF_PREVIEW_WEARABLES, out string? wearablesCsv))
                // Regular flow when no "self preview wearables" are provided:
                return await source.GetAsync(pageSize, pageNumber, ct, sortingField, orderBy, category, collectionType, name, results);

            URN[] pointers = wearablesCsv!.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                          .Select(s => new URN(s))
                                          .ToArray();

            if (appArgs.TryGetValue(AppArgsFlags.SELF_PREVIEW_SOURCE_URL, out string? downloadSourceUrl))
            {
                // TODO: support many
                downloadSourceUrl += $"/collections/{pointers[0]}/items";

                return await source.GetAsync(pageSize, pageNumber, ct, sortingField, orderBy, category, collectionType, name, results,
                    intentionUrl: downloadSourceUrl);
            }

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

        public async UniTask<IReadOnlyCollection<IWearable>?> RequestPointersAsync(IReadOnlyCollection<URN> pointers,
            BodyShape bodyShape,
            CancellationToken ct) =>
                // pass "pointers source" here? from 'self-preview-source-url'... supposedly the Intention should have the CommonArguments
                await source.RequestPointersAsync(pointers, bodyShape, ct);

    }
}
