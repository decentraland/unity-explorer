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
        }

        public void AddPending(string fullUrn)
        {
            if (pendingFullUrns.Add(fullUrn))
                persistence.SavePendingUrns(pendingFullUrns);
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

            return count;
        }

        public void Prune(IReadOnlyDictionary<URN, NftBlockchainOperationEntry> wearableRegistry,
            IReadOnlyDictionary<URN, NftBlockchainOperationEntry> emoteRegistry)
        {
            var toRemove = new List<string>();

            foreach (string? pending in pendingFullUrns)
            {
                // Check if we still own this specific instance
                bool stillOwned = false;

                foreach (var kvp in wearableRegistry)
            {
                    if (kvp.Value.Urn == pending)
                    {
                        stillOwned = true;
                        break;
                    }
                }

                if (!stillOwned)
                {
                    foreach (var kvp in emoteRegistry)
                    {
                        if (kvp.Value.Urn == pending)
                        {
                            stillOwned = true;
                            break;
                        }
                    }
                }

                // If we don't own it anymore, the transfer is
                // done (or failed/gone), remove from pending
                if (!stillOwned)
                    toRemove.Add(pending);
            }

            if (toRemove.Count > 0)
            {
                foreach (string? item in toRemove) pendingFullUrns.Remove(item);
                persistence.SavePendingUrns(pendingFullUrns);
                ReportHub.Log(ReportCategory.GIFTING, $"Pruned {toRemove.Count} confirmed gifts.");
            }
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