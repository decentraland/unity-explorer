using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Wearables.Components;
using DCL.Optimization.PerformanceBudgeting;
using ECS.StreamableLoading.AudioClips;
using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;
using Utility.Multithreading;

namespace DCL.AvatarRendering.Emotes
{
    public class MemoryEmotesStorage : IEmoteStorage
    {
        private readonly LinkedList<(URN key, long lastUsedFrame)> listedCacheKeys = new ();
        private readonly Dictionary<URN, LinkedListNode<(URN key, long lastUsedFrame)>> cacheKeysDictionary = new ();
        private readonly Dictionary<URN, IEmote> emotes = new ();
        private readonly Dictionary<URN, Dictionary<URN, NftBlockchainOperationEntry>> ownedNftsRegistry = new ();

        private readonly object lockObject = new ();

        public List<URN> EmbededURNs { get; } = new ();

        public bool TryGetElement(URN urn, out IEmote element)
        {
            lock (lockObject)
            {
                if (!emotes.TryGetValue(urn, out element))
                    return false;

                UpdateListedCachePriority(urn);

                return true;
            }
        }

        public void Set(URN urn, IEmote element)
        {
            lock (lockObject) { emotes[urn] = element; }
        }


        public void AddEmbeded(URN urn, IEmote emote)
        {
            lock (lockObject)
            {
                EmbededURNs.Add(urn);
                emotes[urn] = emote;
            }
        }

        public IEmote GetOrAddByDTO(EmoteDTO emoteDto, bool qualifiedForUnloading = true)
        {
            lock (lockObject)
            {
                return TryGetElement(emoteDto.metadata.id, out IEmote existingEmote)
                    ? existingEmote
                    : AddEmote(
                        emoteDto.metadata.id,
                        new Emote(
                            new StreamableLoadingResult<EmoteDTO>(emoteDto), false),
                        qualifiedForUnloading
                    );
            }
        }

        public void Unload(IPerformanceBudget frameTimeBudget)
        {
            lock (lockObject)
            {
                for (LinkedListNode<(URN key, long lastUsedFrame)> node = listedCacheKeys.First; frameTimeBudget.TrySpendBudget() && node != null; node = node.Next)
                {
                    URN urn = node.Value.key;

                    if (!emotes.TryGetValue(urn, out IEmote emote))
                        continue;

                    if (!TryUnloadAllWearableAssets(emote)) continue;

                    DisposeThumbnail(emote);
                    DisposeAudioClips(emote);

                    emotes.Remove(urn);
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
                    ownedWearableRegistry = new Dictionary<URN, NftBlockchainOperationEntry>();

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

            for (var i = 0; i < emote.AssetResults.Length; i++)
            {
                StreamableLoadingResult<AttachmentRegularAsset>? result = emote.AssetResults[i];
                AttachmentRegularAsset? wearableAsset = emote.AssetResults[i]?.Asset;

                if (wearableAsset == null || wearableAsset.ReferenceCount == 0)
                {
                    wearableAsset?.Dispose();
                    emote.AssetResults[i] = null;
                }

                // TODO obscure logic - it's not clear what's happening here
                if ((!emote.IsLoading && result == null) || !result.HasValue || result.Value is { Succeeded: true, Asset: null })
                    countNullOrEmpty++;
            }

            return countNullOrEmpty == emote.AssetResults.Length;
        }

        private static void DisposeThumbnail(IEmote wearable)
        {
            if (wearable.ThumbnailAssetResult is { IsInitialized: true })
                wearable.ThumbnailAssetResult.Value.Asset.RemoveReference();
        }

        private static void DisposeAudioClips(IEmote emote)
        {
            foreach (StreamableLoadingResult<AudioClipData>? audioAssetResult in emote.AudioAssetResults)
            {
                if (audioAssetResult is { Succeeded: true })
                    audioAssetResult.Value.Asset!.Dereference();
            }
        }
    }
}
