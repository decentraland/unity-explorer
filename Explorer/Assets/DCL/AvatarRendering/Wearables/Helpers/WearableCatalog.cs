using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables.Components;
using DCL.Optimization.PerformanceBudgeting;
using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;
using Utility.Multithreading;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    public partial class WearableCatalog : IWearableCatalog
    {
        private readonly LinkedList<(string key, long lastUsedFrame)> listedCacheKeys = new ();
        private readonly Dictionary<string, LinkedListNode<(string key, long lastUsedFrame)>> cacheKeysDictionary = new ();
        private readonly Dictionary<URN, Dictionary<URN, NftBlockchainOperationEntry>> ownedNftsRegistry = new ();

        internal Dictionary<string, IWearable> wearablesCache { get; } = new ();

        public IWearable GetOrAddWearableByDTO(WearableDTO wearableDto, bool qualifiedForUnloading = true) =>
            TryGetWearable(wearableDto.metadata.id, out IWearable existingWearable)
                ? existingWearable
                : AddWearable(wearableDto.metadata.id, new Wearable(new StreamableLoadingResult<WearableDTO>(wearableDto)), qualifiedForUnloading);

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

        public bool TryGetWearable(URN wearableURN, out IWearable wearable)
        {
            if (wearablesCache.TryGetValue(wearableURN, out wearable))
            {
                UpdateListedCachePriority(@for: wearableURN);
                return true;
            }

            return false;
        }

        public IWearable GetDefaultWearable(  BodyShape bodyShape, string category)
        {
            var wearableURN =
                WearablesConstants.DefaultWearables.GetDefaultWearable(bodyShape, category);

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

        public void SetOwnedNft(URN nftUrn, NftBlockchainOperationEntry entry)
        {
            if (!ownedNftsRegistry.TryGetValue(nftUrn, out Dictionary<URN, NftBlockchainOperationEntry> ownedWearableRegistry))
            {
                ownedWearableRegistry = new Dictionary<URN, NftBlockchainOperationEntry>();
                ownedNftsRegistry[nftUrn] = ownedWearableRegistry;
            }

            ownedWearableRegistry[entry.Urn] = entry;
        }

        public bool TryGetOwnedNftRegistry(URN nftUrn, out IReadOnlyDictionary<URN, NftBlockchainOperationEntry> registry)
        {
            bool result = ownedNftsRegistry.TryGetValue(nftUrn, out Dictionary<URN, NftBlockchainOperationEntry> r);
            registry = r;
            return result;
        }

        private static bool TryUnloadAllWearableAssets(IWearable wearable)
        {
            var countNullOrEmpty = 0;
            var assetsCount = 0;

            for (var i = 0; i < wearable.WearableAssetResults.Length; i++)
            {
                ref var assets = ref wearable.WearableAssetResults[i];
                assetsCount += assets.Results?.Length ?? 0;

                for (var j = 0; j < assets.Results?.Length; j++)
                {
                    StreamableLoadingResult<WearableAssetBase>? result = assets.Results[i];

                    if (result is not { Succeeded: true })
                    {
                        countNullOrEmpty++;
                        continue;
                    }

                    WearableAssetBase wearableAsset = result.Value.Asset!;

                    if (wearableAsset.ReferenceCount == 0)
                    {
                        wearableAsset?.Dispose();
                        assets.Results[i] = null;
                    }
                }
            }

            return countNullOrEmpty == assetsCount;
        }
    }
}
