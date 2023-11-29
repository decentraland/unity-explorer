using DCL.AvatarRendering.Wearables.Components;
using DCL.PerformanceAndDiagnostics.Optimization.PerformanceBudgeting;
using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    public class WearableCatalog
    {
        internal List<(string key, uint lastUsedFrame)> listedCacheKeys = new ();

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
            if (wearableDictionary.TryGetValue(wearableDto.metadata.id, out IWearable exitingWearable)) { return exitingWearable; }

            var wearable = new Wearable
            {
                WearableDTO = new StreamableLoadingResult<WearableDTO>(wearableDto),
                IsLoading = false,
            };

            wearableDictionary.Add(wearable.GetUrn(), wearable);
            listedCacheKeys.Add((wearable.GetUrn(), 0));

            return wearable;
        }

        public void AddEmptyWearable(string loadingIntentionPointer)
        {
            wearableDictionary.Add(loadingIntentionPointer, new Wearable());
            listedCacheKeys.Add((loadingIntentionPointer, (uint)Time.frameCount));
        }

        public bool TryGetWearable(string wearableURN, out IWearable wearable)
        {
            if (wearableDictionary.TryGetValue(wearableURN, out wearable))
            {
                int tupleIdx = listedCacheKeys.FindIndex(x => x.key == wearableURN);
                listedCacheKeys[tupleIdx] = (wearableURN, (uint)Time.frameCount);

                return true;
            }

            return false;
        }

        public void Unload(IConcurrentBudgetProvider frameTimeBudgetProvider)
        {
            listedCacheKeys.Sort((pair1, pair2) => pair1.lastUsedFrame.CompareTo(pair2.lastUsedFrame));

            for (int i = listedCacheKeys.Count - 1; i >= 0; i--)
            {
                string key = listedCacheKeys[i].key;

                if (wearableDictionary.TryGetValue(listedCacheKeys[i].key, out IWearable wearable))
                {
                    var nullifiedCount = 0;

                    for (var i1 = 0; i1 < wearable.WearableAssets?.Length; i1++)
                    {
                        WearableAsset wearableAsset = wearable.WearableAssets[i1]?.Asset;

                        if (wearableAsset == null)
                        {
                            wearable.WearableAssets[i1] = null;
                            nullifiedCount++;
                        }
                        else if (wearableAsset.ReferenceCount == 0)
                        {
                            wearableAsset.Dispose();
                            wearable.WearableAssets[i1] = null;
                            nullifiedCount++;
                        }
                    }

                    if (nullifiedCount == wearable.WearableAssets?.Length)
                    {
                        wearableDictionary.Remove(key);
                        listedCacheKeys.RemoveAt(i);
                    }
                }
            }
        }

        public IWearable GetDefaultWearable(BodyShape bodyShape, string category) =>
            wearableDictionary[WearablesConstants.DefaultWearables.GetDefaultWearable(bodyShape, category)];
    }
}
