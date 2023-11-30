using DCL.AvatarRendering.Wearables.Components;
using DCL.PerformanceAndDiagnostics.Optimization.PerformanceBudgeting;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Collections.Generic;
using Utility.Multithreading;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    public class WearableCatalog
    {
        private static readonly Comparison<(string key, long lastUsedFrame)> compareByLastUsedFrame =
            (pair1, pair2) => pair1.lastUsedFrame.CompareTo(pair2.lastUsedFrame);

        private readonly List<(string key, long lastUsedFrame)> listedCacheKeys = new ();

        public int WearableAssetsInCatalog
        {
            get
            {
                var sum = 0;

                foreach (IWearable wearable in wearableDictionary.Values)
                {
                    if (wearable.WearableAssets != null)
                    {
                        var count = 0;

                        foreach (StreamableLoadingResult<WearableAsset>? result in wearable.WearableAssets)
                            if (result is { Asset: not null })
                                count++;

                        sum += count;
                    }
                }

                return sum;
            }
        }

        internal Dictionary<string, IWearable> wearableDictionary { get; } = new ();

        public IWearable GetOrAddWearableByDTO(WearableDTO wearableDto)
        {
            if (TryGetWearable(wearableDto.metadata.id, out IWearable existingWearable))
                return existingWearable;

            return AddWearable(wearableDto.metadata.id, new Wearable
            {
                WearableDTO = new StreamableLoadingResult<WearableDTO>(wearableDto),
                IsLoading = false,
            });
        }

        public void AddEmptyWearable(string loadingIntentionPointer) =>
            AddWearable(loadingIntentionPointer, new Wearable());

        private IWearable AddWearable(string loadingIntentionPointer, IWearable wearable)
        {
            wearableDictionary.Add(loadingIntentionPointer, wearable);
            listedCacheKeys.Add((loadingIntentionPointer, MultithreadingUtility.FrameCount));

            return wearable;
        }

        public bool TryGetWearable(string wearableURN, out IWearable wearable)
        {
            if (wearableDictionary.TryGetValue(wearableURN, out wearable))
            {
                UpdateListedCachePriority(@for: wearableURN);
                return true;
            }

            return false;
        }

        public IWearable GetDefaultWearable(BodyShape bodyShape, string category)
        {
            string wearableURN = WearablesConstants.DefaultWearables.GetDefaultWearable(bodyShape, category);

            UpdateListedCachePriority(@for: wearableURN);
            return wearableDictionary[wearableURN];
        }

        private void UpdateListedCachePriority(string @for)
        {
            int tupleIdx = listedCacheKeys.FindIndex(x => x.key == @for);
            listedCacheKeys[tupleIdx] = (@for, MultithreadingUtility.FrameCount);
        }

        public void Unload(IConcurrentBudgetProvider frameTimeBudgetProvider)
        {
            listedCacheKeys.Sort(compareByLastUsedFrame);

            for (var i = 0; frameTimeBudgetProvider.TrySpendBudget() && i < listedCacheKeys.Count; i++)
                if (wearableDictionary.TryGetValue(listedCacheKeys[i].key, out IWearable wearable))
                    UnloadWearableAssets(wearable);
        }

        private static void UnloadWearableAssets(IWearable wearable)
        {
            for (var i = 0; i < wearable.WearableAssets?.Length; i++)
            {
                WearableAsset wearableAsset = wearable.WearableAssets[i]?.Asset;

                if (wearableAsset == null)
                    wearable.WearableAssets[i] = null;
                else if (wearableAsset.ReferenceCount == 0)
                {
                    wearableAsset.Dispose();
                    wearable.WearableAssets[i] = null;
                }
            }
        }
    }
}
