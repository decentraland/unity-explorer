using DCL.AvatarRendering.Wearables.Components;
using DCL.PerformanceAndDiagnostics.Optimization.PerformanceBudgeting;
using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    public class WearableCatalog
    {
        public int WearableAssetsInCatalog
        {
            get
            {
                var sum = 0;

                foreach ((uint LastUsedFrame, IWearable wearable) value in wearableDictionary.Values)
                {
                    if (value.wearable.WearableAssets != null)
                    {
                        var count = 0;

                        foreach (StreamableLoadingResult<WearableAsset>? result in value.wearable.WearableAssets)
                            if (result is { Asset: not null })
                                count++;

                        sum += count;
                    }
                }

                return sum;
            }
        }

        internal Dictionary<string, (uint LastUsedFrame, IWearable wearable)> wearableDictionary { get; } = new ();

        public IWearable GetOrAddWearableByDTO(WearableDTO wearableDto)
        {
            if (wearableDictionary.TryGetValue(wearableDto.metadata.id, out (uint LastUsedFrame, IWearable wearable) exitingWearable))
            {
                wearableDictionary[wearableDto.metadata.id] = (0, exitingWearable.wearable);
                return exitingWearable.wearable;
            }

            var wearable = new Wearable
            {
                WearableDTO = new StreamableLoadingResult<WearableDTO>(wearableDto),
                IsLoading = false,
            };

            wearableDictionary.Add(wearable.GetUrn(), (0, wearable));
            return wearable;
        }

        public void AddEmptyWearable(string loadingIntentionPointer)
        {
            wearableDictionary.Add(loadingIntentionPointer, ((uint)Time.frameCount, new Wearable()));
        }

        public bool TryGetWearable(string wearableURN, out IWearable wearable)
        {
            if (wearableDictionary.TryGetValue(wearableURN, out (uint LastUsedFrame, IWearable wearable) resultWearable))
            {
                wearable = resultWearable.wearable;
                wearableDictionary[wearableURN] = ((uint)Time.frameCount, wearable);

                return true;
            }

            wearable = null;
            return false;
        }

        public void Unload(IConcurrentBudgetProvider frameTimeBudgetProvider)
        {
            using (ListPool<KeyValuePair<string, (uint LastUsedFrame, IWearable Wearable)>>.Get(out List<KeyValuePair<string, (uint LastUsedFrame, IWearable Wearable)>> sortedCache))
            {
                PrepareListSortedByLastUsage(sortedCache);

                foreach (KeyValuePair<string, (uint LastUsedFrame, IWearable Wearable)> pair in sortedCache)
                {
                    if (!frameTimeBudgetProvider.TrySpendBudget()) break;

                    UnloadWearableAssets(pair.Value.Wearable, frameTimeBudgetProvider);
                }
            }

            return;

            void PrepareListSortedByLastUsage(List<KeyValuePair<string, (uint LastUsedFrame, IWearable Wearable)>> sortedCache)
            {
                foreach (KeyValuePair<string, (uint LastUsedFrame, IWearable Wearable)> item in wearableDictionary)
                    sortedCache.Add(item);

                sortedCache.Sort(CompareByLastUsedFrame);
            }
        }

        private static int CompareByLastUsedFrame(KeyValuePair<string, (uint LastUsedFrame, IWearable Wearable)> pair1, KeyValuePair<string, (uint LastUsedFrame, IWearable Wearable)> pair2) =>
            pair1.Value.LastUsedFrame.CompareTo(pair2.Value.LastUsedFrame);

        private static void UnloadWearableAssets(IWearable wearable, IConcurrentBudgetProvider frameTimeBudgetProvider)
        {
            for (var i = 0; i < wearable.WearableAssets?.Length; i++)
            {
                if (!frameTimeBudgetProvider.TrySpendBudget()) break;

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

        public IWearable GetDefaultWearable(BodyShape bodyShape, string category) =>
            wearableDictionary[WearablesConstants.DefaultWearables.GetDefaultWearable(bodyShape, category)].wearable;
    }
}
