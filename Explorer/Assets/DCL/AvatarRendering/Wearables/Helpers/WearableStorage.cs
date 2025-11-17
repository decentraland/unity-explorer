using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using DCL.Optimization.PerformanceBudgeting;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Collections.Generic;
using Utility.Multithreading;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    public partial class WearableStorage : IWearableStorage
    {
        private readonly LinkedList<(URN key, long lastUsedFrame)> listedCacheKeys = new ();
        private readonly Dictionary<URN, LinkedListNode<(URN key, long lastUsedFrame)>> cacheKeysDictionary = new (new Dictionary<URN, LinkedListNode<(URN key, long lastUsedFrame)>>(), URNIgnoreCaseEqualityComparer.Default);

        public Dictionary<URN, Dictionary<URN, NftBlockchainOperationEntry>> AllOwnedNftRegistry { get; } = new (new Dictionary<URN, Dictionary<URN, NftBlockchainOperationEntry>>(), URNIgnoreCaseEqualityComparer.Default);

        private readonly object lockObject = new ();

        internal Dictionary<URN, IWearable> wearablesCache { get; } = new (new Dictionary<URN, IWearable>(), URNIgnoreCaseEqualityComparer.Default);

        public IWearable GetOrAddByDTO(WearableDTO wearableDto, bool qualifiedForUnloading = true)
        {
            lock (lockObject)
            {
                return TryGetElement(wearableDto.metadata.id, out IWearable existingWearable)
                    ? existingWearable
                    : AddWearable(wearableDto.metadata.id, new Wearable(new StreamableLoadingResult<WearableDTO>(wearableDto)), qualifiedForUnloading);
            }
        }

        internal void AddToInternalCache(IWearable wearable)
        {
            wearablesCache.Add(wearable.GetUrn(), wearable);
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

                    DisposeThumbnail(wearable);

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
                if (!AllOwnedNftRegistry.TryGetValue(nftUrn, out Dictionary<URN, NftBlockchainOperationEntry> ownedWearableRegistry))
                {
                    ownedWearableRegistry = new Dictionary<URN, NftBlockchainOperationEntry>(new Dictionary<URN, NftBlockchainOperationEntry>(),
                        URNIgnoreCaseEqualityComparer.Default);

                    AllOwnedNftRegistry[nftUrn] = ownedWearableRegistry;
                }

                ownedWearableRegistry[entry.Urn] = entry;
            }
        }

        public bool TryGetOwnedNftRegistry(URN nftUrn, out IReadOnlyDictionary<URN, NftBlockchainOperationEntry> registry)
        {
            lock (lockObject)
            {
                bool result = AllOwnedNftRegistry.TryGetValue(nftUrn, out Dictionary<URN, NftBlockchainOperationEntry> r);
                registry = r;
                return result;
            }
        }

        public void ClearOwnedNftRegistry()
        {
            lock (lockObject)
            {
                AllOwnedNftRegistry.Clear();
            }
        }

        public bool TryGetLatestTransferredAt(URN nftUrn, out DateTime latestTransferredAt)
        {
            lock (lockObject)
            {
                if (!AllOwnedNftRegistry.TryGetValue(nftUrn, out Dictionary<URN, NftBlockchainOperationEntry> registry) || registry.Count == 0)
                {
                    latestTransferredAt = default;
                    return false;
                }

                DateTime latestDate = DateTime.MinValue;
                
                foreach (var entry in registry.Values)
                {
                    if (entry.TransferredAt > latestDate)
                    {
                        latestDate = entry.TransferredAt;
                    }
                }
                
                latestTransferredAt = latestDate;
                return true;
            }
        }

        public int GetOwnedNftCount(URN nftUrn)
        {
            lock (lockObject)
            {
                if (AllOwnedNftRegistry.TryGetValue(nftUrn, out var registry))
                {
                    return registry.Count;
                }

                return 0;
            }
        }

        public bool TryGetLatestOwnedNft(URN nftUrn, out NftBlockchainOperationEntry entry)
        {
            lock (lockObject)
            {
                entry = default;

                if (!AllOwnedNftRegistry.TryGetValue(nftUrn, out var registry) || registry.Count == 0)
                    return false;

                NftBlockchainOperationEntry best = default;
                bool hasBest = false;

                foreach (var e in registry.Values)
                {
                    if (!hasBest || e.TransferredAt > best.TransferredAt)
                    {
                        best = e;
                        hasBest = true;
                    }
                }

                if (!hasBest)
                    return false;

                entry = best;
                return true;
            }
        }

        internal IWearable AddWearable(URN urn, IWearable wearable, bool qualifiedForUnloading)
        {
            lock (lockObject)
            {
                wearablesCache.Add(urn, wearable);

                if (qualifiedForUnloading)
                    cacheKeysDictionary[urn] =
                        listedCacheKeys.AddLast((urn, MultithreadingUtility.FrameCount));

                return wearable;
            }
        }

        private static bool TryUnloadAllWearableAssets(IWearable wearable)
        {
            var countNullOrEmpty = 0;
            var assetsCount = 0;

            for (var i = 0; i < wearable.WearableAssetResults.Length; i++)
            {
                ref WearableAssets assets = ref wearable.WearableAssetResults[i];
                assetsCount += assets.Results?.Length ?? 0;

                for (var j = 0; j < assets.Results?.Length; j++)
                {
                    StreamableLoadingResult<AttachmentAssetBase>? result = assets.Results[j];

                    if (result is not { Succeeded: true })
                    {
                        countNullOrEmpty++;
                        continue;
                    }

                    AttachmentAssetBase? wearableAsset = result.Value.Asset;

                    if (wearableAsset is { ReferenceCount: 0 })
                    {
                        // TODO it's not clear why countNullOrEmpty is not incremented

                        wearableAsset.Dispose();
                        assets.Results[j] = null;
                    }
                }
            }

            return countNullOrEmpty == assetsCount;
        }

        private static void DisposeThumbnail(IWearable wearable)
        {
            if (wearable.ThumbnailAssetResult is { IsInitialized: true })
                wearable.ThumbnailAssetResult.Value.Asset.RemoveReference();
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
