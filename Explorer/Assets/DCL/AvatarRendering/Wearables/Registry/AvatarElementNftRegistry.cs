using System;
using System.Collections.Generic;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables.Components;

namespace  DCL.AvatarRendering.Wearables.Registry
{
    /// <summary>
    ///     Shared implementation for tracking owned NFTs (Wearables/Emotes)
    /// </summary>
    public abstract class AvatarElementNftRegistry
    {
        // Protected so derived classes can lock on it
        protected readonly object lockObject = new ();

        private readonly Dictionary<URN, Dictionary<URN, NftBlockchainOperationEntry>> ownedNftRegistry
            = new (URNIgnoreCaseEqualityComparer.Default);

        public IReadOnlyDictionary<URN, Dictionary<URN, NftBlockchainOperationEntry>> AllOwnedNftRegistry => ownedNftRegistry;

        public void SetOwnedNft(URN nftUrn, NftBlockchainOperationEntry entry)
        {
            lock (lockObject)
            {
                if (!ownedNftRegistry.TryGetValue(nftUrn, out var registry))
                {
                    registry = new Dictionary<URN, NftBlockchainOperationEntry>(URNIgnoreCaseEqualityComparer.Default);
                    ownedNftRegistry[nftUrn] = registry;
                }

                registry[entry.Urn] = entry;
            }
        }

        public bool TryGetOwnedNftRegistry(URN nftUrn, out IReadOnlyDictionary<URN, NftBlockchainOperationEntry> registry)
        {
            lock (lockObject)
            {
                bool result = ownedNftRegistry.TryGetValue(nftUrn, out var r);
                registry = r;
                return result;
            }
        }

        public void ClearOwnedNftRegistry()
        {
            lock (lockObject)
            {
                ownedNftRegistry.Clear();
            }
        }

        public bool TryGetLatestTransferredAt(URN nftUrn, out DateTime latestTransferredAt)
        {
            lock (lockObject)
            {
                if (!ownedNftRegistry.TryGetValue(nftUrn, out var registry) || registry.Count == 0)
                {
                    latestTransferredAt = default;
                    return false;
                }

                var latestDate = DateTime.MinValue;
                foreach (var entry in registry.Values)
                {
                    if (entry.TransferredAt > latestDate)
                        latestDate = entry.TransferredAt;
                }

                latestTransferredAt = latestDate;
                return true;
            }
        }

        public bool TryGetLatestOwnedNft(URN nftUrn, out NftBlockchainOperationEntry entry)
        {
            lock (lockObject)
            {
                entry = default;
                if (!ownedNftRegistry.TryGetValue(nftUrn, out var registry) || registry.Count == 0)
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

                if (!hasBest) return false;
                entry = best;
                return true;
            }
        }

        public int GetOwnedNftCount(URN nftUrn)
        {
            lock (lockObject)
            {
                return ownedNftRegistry.TryGetValue(nftUrn, out var registry) ? registry.Count : 0;
            }
        }
    }
}