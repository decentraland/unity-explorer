using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using DCL.Optimization.PerformanceBudgeting;
using System;
using System.Collections.Generic;
using Utility.Multithreading;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    public class TrimmedWearableStorage : ITrimmedAvatarElementStorage<ITrimmedWearable, TrimmedWearableDTO>
    {
        private readonly LinkedList<(URN key, long lastUsedFrame)> listedCacheKeys = new ();
        private readonly Dictionary<URN, LinkedListNode<(URN key, long lastUsedFrame)>> cacheKeysDictionary = new (new Dictionary<URN, LinkedListNode<(URN key, long lastUsedFrame)>>(), URNIgnoreCaseEqualityComparer.Default);
        private readonly object lockObject = new ();

        internal Dictionary<URN, ITrimmedWearable> wearablesCache { get; } = new (new Dictionary<URN, ITrimmedWearable>(), URNIgnoreCaseEqualityComparer.Default);

        public bool TryGetElement(URN urn, out ITrimmedWearable element)
        {
            lock (lockObject)
            {
                if (wearablesCache.TryGetValue(urn, out element))
                {
                    UpdateListedCachePriority(@for: urn);
                    return true;
                }

                return false;
            }
        }

        public void Set(URN urn, ITrimmedWearable element)
        {
            lock (lockObject)
            {
                wearablesCache[urn] = element;
                UpdateListedCachePriority(urn);
            }
        }

        public ITrimmedWearable GetOrAddByDTO(TrimmedWearableDTO dto, bool qualifiedForUnloading = true)
        {
            lock (lockObject)
            {
                return TryGetElement(dto.metadata.id, out ITrimmedWearable existingWearable)
                    ? existingWearable
                    : AddWearable(dto.metadata.id, new TrimmedWearable(dto), qualifiedForUnloading);
            }
        }

        public void Unload(IPerformanceBudget frameTimeBudget)
        {
            lock (lockObject)
            {
                for (LinkedListNode<(URN key, long lastUsedFrame)> node = listedCacheKeys.First; frameTimeBudget.TrySpendBudget() && node != null; node = node.Next)
                {
                    URN urn = node.Value.key;

                    if (!wearablesCache.TryGetValue(urn, out ITrimmedWearable wearable))
                        continue;

                    DisposeThumbnail(wearable);

                    wearablesCache.Remove(urn);
                    cacheKeysDictionary.Remove(urn);
                    listedCacheKeys.Remove(node);
                }
            }
        }

        public void SetOwnedNft(URN urn, NftBlockchainOperationEntry operation) =>
            throw new NotImplementedException();

        public bool TryGetOwnedNftRegistry(URN nftUrn, out IReadOnlyDictionary<URN, NftBlockchainOperationEntry> registry) =>
            throw new NotImplementedException();

        public void ClearOwnedNftRegistry() =>
            throw new NotImplementedException();

        private ITrimmedWearable AddWearable(URN urn, ITrimmedWearable wearable, bool qualifiedForUnloading)
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

        private static void DisposeThumbnail(ITrimmedWearable wearable)
        {
            ITrimmedAvatarAttachment attachment = wearable;
            if (attachment.ThumbnailAssetResult is { IsInitialized: true })
                attachment.ThumbnailAssetResult.Value.Asset.RemoveReference();
        }
    }
}
