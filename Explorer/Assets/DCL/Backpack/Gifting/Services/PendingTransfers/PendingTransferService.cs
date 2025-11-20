using System.Collections.Generic;
using System.Text;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables.Components;
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
                // Check if pending starts with baseUrn AND is followed by a separator or end
                // This handles "urn:1" vs "urn:10" correctly
                if (pending.StartsWith(baseUrn) &&
                    (pending.Length == baseUrn.Length || pending[baseUrn.Length] == ':'))
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
            if (pendingFullUrns.Count > 0 && wearableRegistry.Count == 0 && emoteRegistry.Count == 0)
            {
                ReportHub.Log(ReportCategory.GIFTING, "[PendingTransferService] Prune skipped: Inventory registries are empty. Waiting for load.");
                return;
            }
            
            var toRemove = new List<string>();

            foreach (string? pendingUrn in pendingFullUrns)
            {
                // Check if we still own this specific pending item
                bool stillOwned = IsUrnInRegistry(pendingUrn, wearableRegistry) ||
                                  IsUrnInRegistry(pendingUrn, emoteRegistry);

                // If it's not in our owned registry anymore, the transfer finished (or failed/gone)
                if (!stillOwned)
                {
                    ReportHub.Log(ReportCategory.GIFTING, $"[PendingTransferService] Pruning {pendingUrn} - User no longer owns it (Transfer likely confirmed).");
                    toRemove.Add(pendingUrn);
                }
                else
                {
                    ReportHub.Log(ReportCategory.GIFTING, $"[PendingTransferService] Keeping {pendingUrn} - User still owns it in local registry.");
                }
            }

            if (toRemove.Count > 0)
            {
                foreach (string? item in toRemove) pendingFullUrns.Remove(item);
                persistence.SavePendingUrns(pendingFullUrns);
                ReportHub.Log(ReportCategory.GIFTING, $"Pruned {toRemove.Count} confirmed gifts.");
            }
            else
            {
                ReportHub.Log(ReportCategory.GIFTING, "[PendingTransferService] Nothing to prune.");
            }
        }

        private bool IsUrnInRegistry(string pendingFullUrn, IReadOnlyDictionary<URN, Dictionary<URN, NftBlockchainOperationEntry>> registry)
        {
            // We don't know the BaseURN easily from the string without parsing, 
            // so we have to iterate the values (Instances). 
            // This is still fast enough for the occasional Prune call.
            foreach (var instances in registry.Values)
            {
                foreach (var entry in instances.Values)
                {
                    if (entry.Urn == pendingFullUrn) return true;
                }
            }

            return false;
        }

        public void LogPendingTransfers()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Pending Transfers ===");
            foreach (string? urn in pendingFullUrns) sb.AppendLine(urn);
            ReportHub.Log(ReportCategory.GIFTING, sb.ToString());
        }
    }
}