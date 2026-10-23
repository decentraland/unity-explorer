using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables.Components;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.Pool;

namespace DCL.AvatarRendering.Wearables
{
    public static class WearablesProviderExtensions
    {
        /// <summary>
        ///     Walks every page matching <paramref name="parameters" /> into <paramref name="results" />.
        ///     Returns false when the walk stopped before the reported total, which is how a failed page shows up.
        /// </summary>
        public static async UniTask<bool> TryGetAllTrimmedAsync(this IWearablesProvider provider,
            IWearablesProvider.Params parameters,
            CancellationToken ct,
            List<ITrimmedWearable> results,
            int pageSize)
        {
            using PooledObject<List<ITrimmedWearable>> _ = ListPool<ITrimmedWearable>.Get(out List<ITrimmedWearable> page);

            parameters.PageSize = pageSize;
            parameters.PageNumber = 1;
            int total = int.MaxValue;
            var loaded = 0;

            while (loaded < total)
            {
                page.Clear();
                (IReadOnlyList<ITrimmedWearable> pageResults, int pageTotal) = await provider.GetTrimmedByParamsAsync(parameters, ct, page);

                // A failed page reports a total of 0, so the total is only trusted from a page that returned items
                if (pageResults.Count == 0)
                    break;

                total = pageTotal;
                results.AddRange(pageResults);
                loaded += pageResults.Count;
                parameters.PageNumber++;
            }

            return loaded >= total;
        }
    }
}
