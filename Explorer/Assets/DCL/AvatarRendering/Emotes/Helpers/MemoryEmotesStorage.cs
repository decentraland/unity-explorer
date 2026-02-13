using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Wearables.Components;
using DCL.Optimization.PerformanceBudgeting;
using ECS.StreamableLoading.AudioClips;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Collections.Generic;
using DCL.AvatarRendering.Wearables.Registry;
using Utility.Multithreading;
using static DCL.AvatarRendering.Emotes.EmoteComponentsUtils;

namespace DCL.AvatarRendering.Emotes
{
    public class MemoryEmotesStorage : AvatarElementNftRegistry, IEmoteStorage
    {
        private readonly LinkedList<(URN key, long lastUsedFrame)> listedCacheKeys = new ();
        private readonly Dictionary<URN, LinkedListNode<(URN key, long lastUsedFrame)>> cacheKeysDictionary = new (new Dictionary<URN, LinkedListNode<(URN key, long lastUsedFrame)>>(),
            URNIgnoreCaseEqualityComparer.Default);
        private readonly Dictionary<URN, IEmote> emotes = new (new Dictionary<URN, IEmote>(), URNIgnoreCaseEqualityComparer.Default);
        private readonly List<URN> baseEmotesUrns = new ();

        public IReadOnlyList<URN> BaseEmotesUrns => baseEmotesUrns;

        public bool TryGetElement(URN urn, out IEmote element)
        {
            lock (lockObject)
            {
                URN convertedUrn = ConvertLegacyEmoteUrnToOnChain(urn);

                if (!emotes.TryGetValue(convertedUrn, out element))
                    return false;

                UpdateListedCachePriority(convertedUrn);

                return true;
            }
        }

        public void Set(URN urn, IEmote element)
        {
            lock (lockObject)
            {
                URN convertedUrn = ConvertLegacyEmoteUrnToOnChain(urn);
                emotes[convertedUrn] = element;
            }
        }

        public void SetBaseEmotesUrns(IReadOnlyCollection<URN> urns)
        {
            lock (lockObject)
            {
                baseEmotesUrns.Clear();
                baseEmotesUrns.AddRange(urns);
            }
        }

        public IEmote GetOrAddByDTO(EmoteDTO emoteDto, bool qualifiedForUnloading = true)
        {
            lock (lockObject)
            {
                URN convertedUrn = ConvertLegacyEmoteUrnToOnChain(emoteDto.metadata.id);

                if (!emotes.TryGetValue(convertedUrn, out IEmote? emote))
                {
                    emote = new Emote(new StreamableLoadingResult<EmoteDTO>(emoteDto), false);
                    emotes.Add(convertedUrn, emote);
                }

                if (qualifiedForUnloading && !UpdateListedCachePriority(convertedUrn))
                    cacheKeysDictionary[convertedUrn] =
                        listedCacheKeys.AddLast((convertedUrn, MultithreadingUtility.FrameCount));

                return emote;
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

        private bool UpdateListedCachePriority(URN @for)
        {
            if (cacheKeysDictionary.TryGetValue(@for, out LinkedListNode<(URN key, long lastUsedFrame)> node))
            {
                node.Value = (@for, MultithreadingUtility.FrameCount);

                cacheKeysDictionary[@for] = node;
                listedCacheKeys.Remove(node);
                listedCacheKeys.AddLast(node);

                return true;
            }

            return false;
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
