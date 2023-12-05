using DCL.AvatarRendering.Wearables.Components;
using DCL.Optimization.PerformanceBudgeting;
using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;
using Utility.Multithreading;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    public partial class WearableCatalog
    {
        private readonly LinkedList<(string key, long lastUsedFrame)> listedCacheKeys = new ();
        private readonly Dictionary<string, LinkedListNode<(string key, long lastUsedFrame)>> cacheKeysDictionary = new ();

        internal Dictionary<string, IWearable> wearablesCache { get; } = new ();

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
            wearablesCache.Add(loadingIntentionPointer, wearable);
            cacheKeysDictionary[loadingIntentionPointer] = listedCacheKeys.AddLast((loadingIntentionPointer, MultithreadingUtility.FrameCount));

            return wearable;
        }

        public bool TryGetWearable(string wearableURN, out IWearable wearable)
        {
            if (wearablesCache.TryGetValue(wearableURN, out wearable))
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
            return wearablesCache[wearableURN];
        }

        private void UpdateListedCachePriority(string @for)
        {
            if (cacheKeysDictionary.TryGetValue(@for, out LinkedListNode<(string key, long lastUsedFrame)> node))
            {
                node.Value = (@for, MultithreadingUtility.FrameCount);

                listedCacheKeys.Remove(node);
                cacheKeysDictionary[@for] = node;
                listedCacheKeys.AddLast(node);
            }
        }

        public void Unload(IConcurrentBudgetProvider frameTimeBudgetProvider)
        {
            for (LinkedListNode<(string key, long lastUsedFrame)> node = listedCacheKeys.First; frameTimeBudgetProvider.TrySpendBudget() && node != null; node = node.Next)
                if (wearablesCache.TryGetValue(node.Value.key, out IWearable wearable))
                {
                    if (UnloadWearableAssets(wearable))
                    {
                        wearablesCache.Remove(node.Value.key);
                        cacheKeysDictionary.Remove(node.Value.key);
                        listedCacheKeys.Remove(node);
                    }
                }
        }

        private static bool UnloadWearableAssets(IWearable wearable)
        {
            for (var i = 0; i < wearable.WearableAssetResults.Length; i++)
            {
                StreamableLoadingResult<WearableAsset>? result = wearable.WearableAssetResults[i];
                WearableAsset wearableAsset = result?.Asset;

                if (wearableAsset == null)
                    wearable.WearableAssetResults[i] = null;
                else if (wearableAsset.ReferenceCount == 0)
                {
                    wearableAsset.Dispose();
                    wearable.WearableAssetResults[i] = null;
                }
            }

            var j = 0;

            for (var i = 0; i < wearable.WearableAssetResults.Length; i++)
            {
                if (!wearable.IsLoading && wearable.WearableAssetResults[i] == null)
                {
                    j++;
                    continue;
                }

                if (!wearable.WearableAssetResults[i].HasValue)
                {
                    j++;
                    continue;
                }

                if (wearable.WearableAssetResults[i].Value.Succeeded && wearable.WearableAssetResults[i].Value.Asset == null)
                    j++;
            }

            return j == wearable.WearableAssetResults.Length;
        }
    }
}
