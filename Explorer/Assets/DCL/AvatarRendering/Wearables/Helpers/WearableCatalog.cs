using DCL.AvatarRendering.Wearables.Components;
using DCL.PerformanceAndDiagnostics.Optimization.PerformanceBudgeting;
using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    public class WearableCatalog
    {
        internal List<(string key, uint lastUsedFrame)> lastUsedList = new ();

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
            lastUsedList.Add((wearable.GetUrn(), 0));

            return wearable;
        }

        public void AddEmptyWearable(string loadingIntentionPointer)
        {
            wearableDictionary.Add(loadingIntentionPointer, new Wearable());
            lastUsedList.Add((loadingIntentionPointer, (uint)Time.frameCount));
        }

        public bool TryGetWearable(string wearableURN, out IWearable wearable)
        {
            if (wearableDictionary.TryGetValue(wearableURN, out wearable))
            {
                int tupleIdx = lastUsedList.FindIndex(x => x.key == wearableURN);
                lastUsedList[tupleIdx] = (wearableURN, (uint)Time.frameCount);

                return true;
            }

            return false;
        }

        public void Unload(IConcurrentBudgetProvider frameTimeBudgetProvider)
        {
            lastUsedList.Sort((pair1, pair2) => pair1.lastUsedFrame.CompareTo(pair2.lastUsedFrame));

            foreach ((string key, uint _) in lastUsedList)

                UnloadWearableAssets(key);
        }

        private void UnloadWearableAssets(string key)
        {
            if (!wearableDictionary.TryGetValue(key, out IWearable wearable)) return;

            var nullifiedCount = 0;

            for (var i = 0; i < wearable.WearableAssets?.Length; i++)
            {
                WearableAsset wearableAsset = wearable.WearableAssets[i]?.Asset;

                if (wearableAsset == null)
                {
                    wearable.WearableAssets[i] = null;
                    nullifiedCount++;
                }
                else if (wearableAsset.ReferenceCount == 0)
                {
                    wearableAsset.Dispose();
                    wearable.WearableAssets[i] = null;
                    nullifiedCount++;
                }
            }

            if (nullifiedCount == wearable.WearableAssets?.Length)
            {
                wearableDictionary.Remove(key);

                // int tupleIdx = lastUsedList.FindIndex(x => x.key == key);
                // lastUsedList.Remove(lastUsedList[tupleIdx]);
            }
        }

        public IWearable GetDefaultWearable(BodyShape bodyShape, string category) =>
            wearableDictionary[WearablesConstants.DefaultWearables.GetDefaultWearable(bodyShape, category)];
    }
}
