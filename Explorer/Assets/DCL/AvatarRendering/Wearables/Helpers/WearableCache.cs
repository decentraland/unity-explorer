using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using DCL.Optimization.PerformanceBudgeting;
using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;
using Utility.Multithreading;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    public partial class WearableCache : IWearableCache
    {
        private readonly LinkedList<(URN key, long lastUsedFrame)> listedCacheKeys = new ();
        private readonly Dictionary<URN, LinkedListNode<(URN key, long lastUsedFrame)>> cacheKeysDictionary = new (new Dictionary<URN, LinkedListNode<(URN key, long lastUsedFrame)>>(), URNIgnoreCaseEqualityComparer.Default);
        private readonly Dictionary<URN, Dictionary<URN, NftBlockchainOperationEntry>> ownedNftsRegistry = new (new Dictionary<URN, Dictionary<URN, NftBlockchainOperationEntry>>(), URNIgnoreCaseEqualityComparer.Default);

        internal Dictionary<URN, IWearable> wearablesCache { get; } = new (new Dictionary<URN, IWearable>(), URNIgnoreCaseEqualityComparer.Default);

        private readonly object lockObject = new ();

        public IWearable GetOrAddByDTO(WearableDTO wearableDto, bool qualifiedForUnloading = true)
        {
            lock (lockObject)
            {
                return TryGetElement(wearableDto.metadata.id, out IWearable existingWearable)
                    ? existingWearable
                    : AddWearable(wearableDto.metadata.id, new Wearable(new StreamableLoadingResult<WearableDTO>(wearableDto)), qualifiedForUnloading);
            }
        }

        public void AddEmptyWearable(URN urn, bool qualifiedForUnloading = true)
        {
            lock (lockObject) { AddWearable(urn, new Wearable(), qualifiedForUnloading); }
        }

        public void Set(URN urn, IWearable element)
        {
            lock (lockObject)
            {
                wearablesCache[urn] = element;
                UpdateListedCachePriority(urn);
            }
        }

        public bool TryGetElement(URN wearableURN, out IWearable wearable)
        {
            lock (lockObject)
            {
                if (wearablesCache.TryGetValue(wearableURN, out wearable))
                {
                    UpdateListedCachePriority(@for: wearableURN);
                    return true;
                }

                return false;
            }
        }

        public IWearable? GetDefaultWearable(BodyShape bodyShape, string category)
        {
            lock (lockObject)
            {
                string wearableURN = WearablesConstants.DefaultWearables.GetDefaultWearable(bodyShape, category);

                UpdateListedCachePriority(wearableURN);

                if (wearablesCache.TryGetValue(wearableURN, out IWearable wearable))
                    return wearable;

                if (wearablesCache.TryGetValue(wearableURN, out wearable))
                    return wearable;

                return null;
            }
        }

        public void Unload(IPerformanceBudget frameTimeBudget)
        {
            lock (lockObject)
            {
                for (LinkedListNode<(URN key, long lastUsedFrame)> node = listedCacheKeys.First; frameTimeBudget.TrySpendBudget() && node != null; node = node.Next)
                {
                    URN urn = node.Value.key;

                    if (!wearablesCache.TryGetValue(urn, out IWearable wearable))
                        continue;

                    if (!TryUnloadAllWearableAssets(wearable)) continue;

                    wearablesCache.Remove(urn);
                    cacheKeysDictionary.Remove(urn);
                    listedCacheKeys.Remove(node);
                }
            }
        }

        public void SetOwnedNft(URN nftUrn, NftBlockchainOperationEntry entry)
        {
            lock (lockObject)
            {
                if (!ownedNftsRegistry.TryGetValue(nftUrn, out Dictionary<URN, NftBlockchainOperationEntry> ownedWearableRegistry))
                {
                    ownedWearableRegistry = new Dictionary<URN, NftBlockchainOperationEntry>(new Dictionary<URN, NftBlockchainOperationEntry>(),
                        URNIgnoreCaseEqualityComparer.Default);

                    ownedNftsRegistry[nftUrn] = ownedWearableRegistry;
                }

                ownedWearableRegistry[entry.Urn] = entry;
            }
        }

        public bool TryGetOwnedNftRegistry(URN nftUrn, out IReadOnlyDictionary<URN, NftBlockchainOperationEntry> registry)
        {
            lock (lockObject)
            {
                bool result = ownedNftsRegistry.TryGetValue(nftUrn, out Dictionary<URN, NftBlockchainOperationEntry> r);
                registry = r;
                return result;
            }
        }

        internal IWearable AddWearable(URN urn, IWearable wearable, bool qualifiedForUnloading)
        {
            wearablesCache.Add(urn, wearable);

            if (qualifiedForUnloading)
                cacheKeysDictionary[urn] =
                    listedCacheKeys.AddLast((urn, MultithreadingUtility.FrameCount));

            return wearable;
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
                    StreamableLoadingResult<WearableAssetBase>? result = assets.Results[j];

                    if (result is not { Succeeded: true })
                    {
                        countNullOrEmpty++;
                        continue;
                    }

                    WearableAssetBase? wearableAsset = result.Value.Asset;

                    if (wearableAsset is { ReferenceCount: 0 })
                    {
                        wearableAsset.Dispose();
                        assets.Results[j] = null;
                    }
                }
            }

            return countNullOrEmpty == assetsCount;
        }

        private void UpdateListedCachePriority(URN @for)
        {
            if (cacheKeysDictionary.TryGetValue(@for, out LinkedListNode<(URN key, long lastUsedFrame)> node))
            {
                node.Value = (@for, MultithreadingUtility.FrameCount);

                cacheKeysDictionary[@for] = node;
                listedCacheKeys.Remove(node);
                listedCacheKeys.AddLast(node);
            }
        }
    }
}
