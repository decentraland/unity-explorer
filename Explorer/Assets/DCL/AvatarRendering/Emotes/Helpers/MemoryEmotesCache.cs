using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Optimization.PerformanceBudgeting;
using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;
using Utility.Multithreading;

namespace DCL.AvatarRendering.Emotes
{
    public class MemoryEmotesCache : IEmoteCache
    {
        private readonly LinkedList<(URN key, long lastUsedFrame)> listedCacheKeys = new ();
        private readonly Dictionary<URN, LinkedListNode<(URN key, long lastUsedFrame)>> cacheKeysDictionary = new (new Dictionary<URN, LinkedListNode<(URN key, long lastUsedFrame)>>(),
            URNIgnoreCaseEqualityComparer.Default);
        private readonly Dictionary<URN, IEmote> emotes = new (new Dictionary<URN, IEmote>(), URNIgnoreCaseEqualityComparer.Default);
        private readonly Dictionary<URN, Dictionary<URN, NftBlockchainOperationEntry>> ownedNftsRegistry = new (new Dictionary<URN, Dictionary<URN, NftBlockchainOperationEntry>>(),
            URNIgnoreCaseEqualityComparer.Default);

        public bool TryGetEmote(URN urn, out IEmote emote)
        {
            if (!emotes.TryGetValue(urn, out emote))
                return false;

            UpdateListedCachePriority(urn);

            return true;
        }

        public void Set(URN urn, IEmote emote) =>
            emotes[urn] = emote;

        public IEmote GetOrAddEmoteByDTO(EmoteDTO emoteDto, bool qualifiedForUnloading = true) =>
            TryGetEmote(emoteDto.metadata.id, out IEmote existingEmote)
                ? existingEmote
                : AddEmote(emoteDto.metadata.id, new Emote
                {
                    Model = new StreamableLoadingResult<EmoteDTO>(emoteDto),
                    IsLoading = false,
                }, qualifiedForUnloading);

        public void Unload(IPerformanceBudget frameTimeBudget)
        {
            for (LinkedListNode<(URN key, long lastUsedFrame)> node = listedCacheKeys.First; frameTimeBudget.TrySpendBudget() && node != null; node = node.Next)
            {
                URN urn = node.Value.key;

                if (!emotes.TryGetValue(urn, out IEmote emote))
                    continue;

                if (!TryUnloadAllWearableAssets(emote)) continue;

                emotes.Remove(urn);
                cacheKeysDictionary.Remove(urn);
                listedCacheKeys.Remove(node);
            }
        }

        public void SetOwnedNft(URN nftUrn, NftBlockchainOperationEntry entry)
        {
            if (!ownedNftsRegistry.TryGetValue(nftUrn, out Dictionary<URN, NftBlockchainOperationEntry> ownedWearableRegistry))
            {
                ownedWearableRegistry = new Dictionary<URN, NftBlockchainOperationEntry>(new Dictionary<URN, NftBlockchainOperationEntry>(),
                    URNIgnoreCaseEqualityComparer.Default);
                ownedNftsRegistry[nftUrn] = ownedWearableRegistry;
            }

            ownedWearableRegistry[entry.Urn] = entry;
        }

        public bool TryGetOwnedNftRegistry(URN nftUrn, out IReadOnlyDictionary<URN, NftBlockchainOperationEntry> registry)
        {
            bool result = ownedNftsRegistry.TryGetValue(nftUrn, out Dictionary<URN, NftBlockchainOperationEntry> r);

            if (!result)
                ownedNftsRegistry.TryGetValue(nftUrn, out r);

            registry = r;

            return result;
        }

        private IEmote AddEmote(URN urn, IEmote wearable, bool qualifiedForUnloading)
        {
            emotes.Add(urn, wearable);

            if (qualifiedForUnloading)
                cacheKeysDictionary[urn] =
                    listedCacheKeys.AddLast((urn, MultithreadingUtility.FrameCount));

            return wearable;
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

        private static bool TryUnloadAllWearableAssets(IEmote emote)
        {
            var countNullOrEmpty = 0;

            for (var i = 0; i < emote.WearableAssetResults.Length; i++)
            {
                StreamableLoadingResult<WearableRegularAsset>? result = emote.WearableAssetResults[i];
                WearableRegularAsset? wearableAsset = emote.WearableAssetResults[i]?.Asset;

                if (wearableAsset == null || wearableAsset.ReferenceCount == 0)
                {
                    wearableAsset?.Dispose();
                    emote.WearableAssetResults[i] = null;
                }

                if ((!emote.IsLoading && result == null) || !result.HasValue || result.Value is { Succeeded: true, Asset: null })
                    countNullOrEmpty++;
            }

            return countNullOrEmpty == emote.WearableAssetResults.Length;
        }
    }
}
