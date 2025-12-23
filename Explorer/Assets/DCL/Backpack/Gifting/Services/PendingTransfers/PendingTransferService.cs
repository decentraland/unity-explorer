using System.Collections.Generic;
using System.Text;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables.Components;
using DCL.Backpack.Gifting.Utils;
using DCL.Diagnostics;

namespace DCL.Backpack.Gifting.Services.PendingTransfers
{
    public class PendingTransferService : IPendingTransferService
    {
        private readonly IGiftingPersistence persistence;
        private readonly HashSet<string> pendingFullUrns;

        public PendingTransferService(IGiftingPersistence persistence)
        {
            this.persistence = persistence;
            pendingFullUrns = persistence.LoadPendingUrns();

            ReportHub.Log(ReportCategory.GIFTING, $"[PendingTransferService] Loaded {pendingFullUrns.Count} items from disk.");
            foreach (string? urn in pendingFullUrns)
                ReportHub.Log(ReportCategory.GIFTING, $"  - Loaded Pending: {urn}");
        }

        public void AddPending(string fullUrn)
        {
            if (pendingFullUrns.Add(fullUrn))
            {
                ReportHub.Log(ReportCategory.GIFTING, $"[PendingTransferService] Adding new pending item: {fullUrn}");
                persistence.SavePendingUrns(pendingFullUrns);
            }
            else
            {
                ReportHub.Log(ReportCategory.GIFTING, $"[PendingTransferService] Item already exists in pending: {fullUrn}");
            }
        }

        public bool IsPending(string fullUrn)
        {
            return pendingFullUrns.Contains(fullUrn);
        }

        public int GetPendingCount(string baseUrn)
        {
            int count = 0;
            foreach (string pending in pendingFullUrns)
            {
                if (GiftingUrnParsingHelper.TryGetBaseUrn(pending, out string extractedBase) &&
                    extractedBase == baseUrn)
                {
                    count++;
                }
            }

            ReportHub.Log(ReportCategory.GIFTING, $"[PendingTransferService] GetPendingCount for {baseUrn}: {count}");
            return count;
        }

        
        public void Prune(
            IReadOnlyDictionary<URN, Dictionary<URN, NftBlockchainOperationEntry>> wearableRegistry,
            IReadOnlyDictionary<URN, Dictionary<URN, NftBlockchainOperationEntry>> emoteRegistry)
        {
            if (pendingFullUrns.Count == 0) return;

            if (wearableRegistry.Count == 0 &&
                emoteRegistry.Count == 0) return;

            var toRemove = new List<string>();

            foreach (string pendingUrn in pendingFullUrns)
            {
                if (!GiftingUrnParsingHelper.TryGetBaseUrn(pendingUrn, out string baseUrnString))
                {
                    toRemove.Add(pendingUrn);
                    continue;
                }

                var baseUrn = new URN(baseUrnString);
                var fullUrnKey = new URN(pendingUrn);

                bool stillOwned = false;

                // O(1) Lookups
                if (wearableRegistry.TryGetValue(baseUrn, out var wInstances) && wInstances.ContainsKey(fullUrnKey))
                {
                    stillOwned = true;
                }
                else if (emoteRegistry.TryGetValue(baseUrn, out var eInstances) && eInstances.ContainsKey(fullUrnKey))
                {
                    stillOwned = true;
                }

                // Validates pending transfers against the latest inventory data.
                // If an item is no longer in the registry, it means the Indexer has caught up 
                // and the item has left the user's wallet. We can safely stop tracking it locally.
                if (!stillOwned)
                    toRemove.Add(pendingUrn);
            }

            if (toRemove.Count > 0)
            {
                foreach (string item in toRemove)
                    pendingFullUrns.Remove(item);

                persistence.SavePendingUrns(pendingFullUrns);
                ReportHub.Log(ReportCategory.GIFTING, $"Pruned {toRemove.Count} confirmed gifts.");
            }
        }

        public void LogPendingTransfers()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Pending Transfers:");
            foreach (string? urn in pendingFullUrns) sb.AppendLine(urn);
            ReportHub.Log(ReportCategory.GIFTING, sb.ToString());
        }
    }
}