﻿using DCL.AvatarRendering.Wearables.Components;
using DCL.Optimization.PerformanceBudgeting;
using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;
using Google.Type;
using Utility.Multithreading;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    public partial class WearableCatalog : IWearableCatalog
    {
        private readonly LinkedList<(string key, long lastUsedFrame)> listedCacheKeys = new ();
        private readonly Dictionary<string, LinkedListNode<(string key, long lastUsedFrame)>> cacheKeysDictionary = new ();

        internal Dictionary<string, IWearable> wearablesCache { get; } = new ();

        public IWearable GetOrAddWearableByDTO(WearableDTO wearableDto, bool qualifiedForUnloading = true)
        {
            return TryGetWearable(wearableDto.metadata.id, out IWearable existingWearable)
                ? existingWearable
                : AddWearable(wearableDto.metadata.id, new Wearable
                {
                    WearableDTO = new StreamableLoadingResult<WearableDTO>(wearableDto),
                    IsLoading = false
                }, qualifiedForUnloading);
        }

        public void AddEmptyWearable(string loadingIntentionPointer, bool qualifiedForUnloading = true)
        {
            AddWearable(loadingIntentionPointer, new Wearable(), qualifiedForUnloading);
        }

        internal IWearable AddWearable(string loadingIntentionPointer, IWearable wearable, bool qualifiedForUnloading)
        {
            wearablesCache.Add(loadingIntentionPointer, wearable);
            if (qualifiedForUnloading)
                cacheKeysDictionary[loadingIntentionPointer] =
                    listedCacheKeys.AddLast((loadingIntentionPointer, MultithreadingUtility.FrameCount));
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

        public IWearable GetDefaultWearable(BodyShape bodyShape, string category, out bool hasEmptyDefaultWearableAB)
        {
            var wearableURN =
                WearablesConstants.DefaultWearables.GetDefaultWearable(bodyShape, category,
                    out hasEmptyDefaultWearableAB);

            UpdateListedCachePriority(@for: wearableURN);
            return wearablesCache[wearableURN];
        }

        private void UpdateListedCachePriority(string @for)
        {
            if (cacheKeysDictionary.TryGetValue(@for, out LinkedListNode<(string key, long lastUsedFrame)> node))
            {
                node.Value = (@for, MultithreadingUtility.FrameCount);

                cacheKeysDictionary[@for] = node;
                listedCacheKeys.Remove(node);
                listedCacheKeys.AddLast(node);
            }
        }

        public void Unload(IPerformanceBudget frameTimeBudget)
        {
            for (LinkedListNode<(string key, long lastUsedFrame)> node = listedCacheKeys.First; frameTimeBudget.TrySpendBudget() && node != null; node = node.Next)
                if (wearablesCache.TryGetValue(node.Value.key, out IWearable wearable))
                    if (TryUnloadAllWearableAssets(wearable))
                    {
                        wearablesCache.Remove(node.Value.key);
                        cacheKeysDictionary.Remove(node.Value.key);
                        listedCacheKeys.Remove(node);
                    }
        }

        private static bool TryUnloadAllWearableAssets(IWearable wearable)
        {
            var countNullOrEmpty = 0;

            for (var i = 0; i < wearable.WearableAssetResults.Length; i++)
            {
                StreamableLoadingResult<WearableAsset>? result = wearable.WearableAssetResults[i];
                WearableAsset wearableAsset = wearable.WearableAssetResults[i]?.Asset;

                if (wearableAsset == null || wearableAsset.ReferenceCount == 0)
                {
                    wearableAsset?.Dispose();
                    wearable.WearableAssetResults[i] = null;
                }

                if ((!wearable.IsLoading && result == null) || !result.HasValue || result.Value is { Succeeded: true, Asset: null })
                    countNullOrEmpty++;
            }

            return countNullOrEmpty == wearable.WearableAssetResults.Length;
        }
    }
}
