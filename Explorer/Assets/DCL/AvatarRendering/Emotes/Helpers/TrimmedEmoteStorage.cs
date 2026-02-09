using CommunicationData.URLHelpers;
using DCL.Optimization.PerformanceBudgeting;
using System.Collections.Generic;
using Utility.Multithreading;

namespace DCL.AvatarRendering.Emotes
{
    public class TrimmedEmoteStorage : ITrimmedEmoteStorage
    {
        private readonly LinkedList<(URN key, long lastUsedFrame)> listedCacheKeys = new ();
        private readonly Dictionary<URN, LinkedListNode<(URN key, long lastUsedFrame)>> cacheKeysDictionary = new (new Dictionary<URN, LinkedListNode<(URN key, long lastUsedFrame)>>(), URNIgnoreCaseEqualityComparer.Default);
        private readonly object lockObject = new ();

        internal Dictionary<URN, ITrimmedEmote> emotesCache { get; } = new (new Dictionary<URN, ITrimmedEmote>(), URNIgnoreCaseEqualityComparer.Default);

        public bool TryGetElement(URN urn, out ITrimmedEmote element)
        {
            lock (lockObject)
            {
                if (emotesCache.TryGetValue(urn, out element))
                {
                    UpdateListedCachePriority(@for: urn);
                    return true;
                }

                return false;
            }
        }

        public ITrimmedEmote GetOrAddByDTO(TrimmedEmoteDTO dto, bool qualifiedForUnloading = true)
        {
            lock (lockObject)
            {
                return TryGetElement(dto.metadata.id, out var existingEmote)
                    ? existingEmote
                    : AddWearable(dto.metadata.id, new TrimmedEmote(dto), qualifiedForUnloading);
            }
        }

        public void Unload(IPerformanceBudget frameTimeBudget)
        {
            lock (lockObject)
            {
                for (var node = listedCacheKeys.First; frameTimeBudget.TrySpendBudget() && node != null; node = node.Next)
                {
                    var urn = node.Value.key;

                    if (!emotesCache.TryGetValue(urn, out var wearable))
                        continue;

                    DisposeThumbnail(wearable);

                    emotesCache.Remove(urn);
                    cacheKeysDictionary.Remove(urn);
                    listedCacheKeys.Remove(node);
                }
            }
        }

        private ITrimmedEmote AddWearable(URN urn, ITrimmedEmote wearable, bool qualifiedForUnloading)
        {
            lock (lockObject)
            {
                emotesCache.Add(urn, wearable);

                if (qualifiedForUnloading)
                    cacheKeysDictionary[urn] =
                        listedCacheKeys.AddLast((urn, MultithreadingUtility.FrameCount));

                return wearable;
            }
        }

        private void UpdateListedCachePriority(URN @for)
        {
            if (cacheKeysDictionary.TryGetValue(@for, out var node))
            {
                node.Value = (@for, MultithreadingUtility.FrameCount);

                cacheKeysDictionary[@for] = node;
                listedCacheKeys.Remove(node);
                listedCacheKeys.AddLast(node);
            }
        }

        private static void DisposeThumbnail(ITrimmedEmote wearable)
        {
            if (wearable.ThumbnailAssetResult is { IsInitialized: true })
                wearable.ThumbnailAssetResult.Value.Asset.RemoveReference();
        }
    }
}
