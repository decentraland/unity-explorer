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

        internal Dictionary<URN, IWearable> wearablesCache { get; } = new ();

        public IWearable GetOrAddWearableByDTO(WearableDTO wearableDto, bool qualifiedForUnloading = true) =>
            TryGetWearable(wearableDto.metadata.id, out IWearable existingWearable)
                ? existingWearable
                : AddWearable(wearableDto.metadata.id, new Wearable(new StreamableLoadingResult<WearableDTO>(wearableDto)), qualifiedForUnloading);

        public void AddEmptyWearable(URN urn, bool qualifiedForUnloading = true)
        {
            AddWearable(urn, new Wearable(), qualifiedForUnloading);
        }

        internal IWearable AddWearable(URN urn, IWearable wearable, bool qualifiedForUnloading)
        {
            // Lower all urn since the server returns urns with lower caps or upper caps representing the same content on different endpoints
            // For example a wearable in the profile (/lambdas/profiles/:address):
            // urn:decentraland:matic:collections-thirdparty:dolcegabbana-disco-drip:0x4bD77619a75C8EdA181e3587339E7011DA75bF0E:2a424e9c-c6fb-4783-99ed-63d260d90ed2
            // The same wearable in the content server (/content/entities/active):
            // urn:decentraland:matic:collections-thirdparty:dolcegabbana-disco-drip:0x4bd77619a75c8eda181e3587339e7011da75bf0e:2a424e9c-c6fb-4783-99ed-63d260d90ed2
            urn = urn.ToLower();

            wearablesCache.Add(urn, wearable);

            if (qualifiedForUnloading)
                cacheKeysDictionary[urn] =
                    listedCacheKeys.AddLast((urn, MultithreadingUtility.FrameCount));

            return wearable;
        }

        public bool TryGetWearable(URN wearableURN, out IWearable wearable)
        {
            if (wearablesCache.TryGetValue(wearableURN, out wearable))
            {
                UpdateListedCachePriority(@for: wearableURN);
                return true;
            }

            // Lower all urn since the server returns urns with lower caps or upper caps representing the same content on different endpoints
            // For example a wearable in the profile (/lambdas/profiles/:address):
            // urn:decentraland:matic:collections-thirdparty:dolcegabbana-disco-drip:0x4bD77619a75C8EdA181e3587339E7011DA75bF0E:2a424e9c-c6fb-4783-99ed-63d260d90ed2
            // The same wearable in the content server (/content/entities/active):
            // urn:decentraland:matic:collections-thirdparty:dolcegabbana-disco-drip:0x4bd77619a75c8eda181e3587339e7011da75bf0e:2a424e9c-c6fb-4783-99ed-63d260d90ed2
            if (wearablesCache.TryGetValue(wearableURN.ToLower(), out wearable))
            {
                UpdateListedCachePriority(@for: wearableURN);
                return true;
            }

            return false;
        }

        public IWearable GetDefaultWearable(BodyShape bodyShape, string category)
        {
            string wearableURN = WearablesConstants.DefaultWearables.GetDefaultWearable(bodyShape, category);

            UpdateListedCachePriority(wearableURN);

            if (wearablesCache.TryGetValue(wearableURN, out IWearable wearable))
                return wearable;

            // Lower all urn since the server returns urns with lower caps or upper caps representing the same content on different endpoints
            // For example a wearable in the profile (/lambdas/profiles/:address):
            // urn:decentraland:matic:collections-thirdparty:dolcegabbana-disco-drip:0x4bD77619a75C8EdA181e3587339E7011DA75bF0E:2a424e9c-c6fb-4783-99ed-63d260d90ed2
            // The same wearable in the content server (/content/entities/active):
            // urn:decentraland:matic:collections-thirdparty:dolcegabbana-disco-drip:0x4bd77619a75c8eda181e3587339e7011da75bf0e:2a424e9c-c6fb-4783-99ed-63d260d90ed2
            if (wearablesCache.TryGetValue(wearableURN.ToLower(), out wearable))
                return wearable;

            return null;
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
            {
                string urn = node.Value.key;

                if (!wearablesCache.TryGetValue(urn, out IWearable wearable))
                    if (wearablesCache.TryGetValue(urn.ToLower(), out wearable))
                        urn = urn.ToLower();

                if (wearable == null) continue;
                if (!TryUnloadAllWearableAssets(wearable)) continue;

                wearablesCache.Remove(urn);
                cacheKeysDictionary.Remove(urn);
                listedCacheKeys.Remove(node);
            }
        }

        public void SetOwnedNft(URN nftUrn, NftBlockchainOperationEntry entry)
        {
            // Lower all urn since the server returns urns with lower caps or upper caps representing the same content on different endpoints
            // For example a wearable in the profile (/lambdas/profiles/:address):
            // urn:decentraland:matic:collections-thirdparty:dolcegabbana-disco-drip:0x4bD77619a75C8EdA181e3587339E7011DA75bF0E:2a424e9c-c6fb-4783-99ed-63d260d90ed2
            // The same wearable in the content server (/content/entities/active):
            // urn:decentraland:matic:collections-thirdparty:dolcegabbana-disco-drip:0x4bd77619a75c8eda181e3587339e7011da75bf0e:2a424e9c-c6fb-4783-99ed-63d260d90ed2
            nftUrn = nftUrn.ToLower();

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

            if (!result)
                ownedNftsRegistry.TryGetValue(nftUrn.ToLower(), out r);

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
    }
}
