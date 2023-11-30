using DCL.AvatarRendering.Wearables.Components;
using DCL.PerformanceAndDiagnostics.Optimization.PerformanceBudgeting;
using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;
using Utility.Multithreading;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    public partial class WearableCatalog
    {
        private readonly LinkedList<(string key, long lastUsedFrame)> listedCacheKeys = new ();
        private readonly Dictionary<string, LinkedListNode<(string key, long lastUsedFrame)>> cacheKeysDictionary = new ();

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
            cacheKeysDictionary[loadingIntentionPointer] = listedCacheKeys.AddLast((loadingIntentionPointer, MultithreadingUtility.FrameCount));

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
            if (cacheKeysDictionary.TryGetValue(@for, out LinkedListNode<(string key, long lastUsedFrame)> node))
            {
                listedCacheKeys.Remove(node);
                cacheKeysDictionary[@for] = listedCacheKeys.AddLast((@for, MultithreadingUtility.FrameCount));
            }
        }

        public void Unload(IConcurrentBudgetProvider frameTimeBudgetProvider)
        {
            for (LinkedListNode<(string key, long lastUsedFrame)> node = listedCacheKeys.First; frameTimeBudgetProvider.TrySpendBudget() && node != null; node = node.Next)
                if (wearableDictionary.TryGetValue(node.Value.key, out IWearable wearable))
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
