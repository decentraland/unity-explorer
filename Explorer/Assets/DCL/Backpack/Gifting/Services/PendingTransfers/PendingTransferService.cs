using System;
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
        private const double TIMEOUT_HOURS = 1.0;

        private readonly IGiftingPersistence persistence;
        private readonly Dictionary<string, PendingTransferEntry> pendingWearables;
        private readonly Dictionary<string, PendingTransferEntry> pendingEmotes;

        public PendingTransferService(IGiftingPersistence persistence)
        {
            this.persistence = persistence;
            
            var (wearables, emotes) = persistence.LoadPendingTransfers();
            pendingWearables = wearables;
            pendingEmotes = emotes;

            ReportHub.Log(ReportCategory.GIFTING, 
                $"[PendingTransferService] Loaded {pendingWearables.Count} wearables and {pendingEmotes.Count} emotes from disk.");
            
            foreach (var entry in pendingWearables.Values)
                ReportHub.Log(ReportCategory.GIFTING, $"  - Wearable: {entry}");
            foreach (var entry in pendingEmotes.Values)
                ReportHub.Log(ReportCategory.GIFTING, $"  - Emote: {entry}");
        }

        public void AddPendingWearable(string fullUrn)
        {
            var entry = new PendingTransferEntry(fullUrn, DateTime.UtcNow);
            
            if (pendingWearables.TryAdd(fullUrn, entry))
            {
                ReportHub.Log(ReportCategory.GIFTING, $"[PendingTransferService] Added pending wearable: {entry}");
                Save();
            }
            else
            {
                ReportHub.Log(ReportCategory.GIFTING, $"[PendingTransferService] Wearable already pending: {fullUrn}");
            }
        }

        public void AddPendingEmote(string fullUrn)
        {
            var entry = new PendingTransferEntry(fullUrn, DateTime.UtcNow);
            
            if (pendingEmotes.TryAdd(fullUrn, entry))
            {
                ReportHub.Log(ReportCategory.GIFTING, $"[PendingTransferService] Added pending emote: {entry}");
                Save();
            }
            else
            {
                ReportHub.Log(ReportCategory.GIFTING, $"[PendingTransferService] Emote already pending: {fullUrn}");
            }
        }

        public bool IsPending(string fullUrn)
        {
            return pendingWearables.ContainsKey(fullUrn) || pendingEmotes.ContainsKey(fullUrn);
        }

        public int GetPendingCount(string baseUrn)
        {
            int count = 0;
            
            count += CountMatchingBase(pendingWearables.Values, baseUrn);
            count += CountMatchingBase(pendingEmotes.Values, baseUrn);

            return count;
        }

        private static int CountMatchingBase(IEnumerable<PendingTransferEntry> entries, string baseUrn)
        {
            int count = 0;
            foreach (var entry in entries)
            {
                if (GiftingUrnParsingHelper.TryGetBaseUrn(entry.FullUrn, out string extractedBase) &&
                    string.Equals(extractedBase, baseUrn, StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                }
            }
            return count;
        }

        public void PruneWearables(
            IReadOnlyDictionary<URN, Dictionary<URN, NftBlockchainOperationEntry>> wearableRegistry)
        {
            if (pendingWearables.Count == 0) return;
            if (wearableRegistry.Count == 0)
            {
                ReportHub.Log(ReportCategory.GIFTING, "[Prune] Wearable registry empty, skipping prune.");
                return;
            }

            ReportHub.Log(ReportCategory.GIFTING, 
                $"[Prune] Pruning wearables. Pending: {pendingWearables.Count}, Registry entries: {wearableRegistry.Count}");

            int pruned = PruneFromRegistry(pendingWearables, wearableRegistry, "Wearable");
            
            if (pruned > 0)
            {
                Save();
                ReportHub.Log(ReportCategory.GIFTING, $"[Prune] Pruned {pruned} wearables.");
            }
        }

        public void PruneEmotes(
            IReadOnlyDictionary<URN, Dictionary<URN, NftBlockchainOperationEntry>> emoteRegistry)
        {
            if (pendingEmotes.Count == 0) return;
            if (emoteRegistry.Count == 0)
            {
                ReportHub.Log(ReportCategory.GIFTING, "[Prune] Emote registry empty, skipping prune.");
                return;
            }

            ReportHub.Log(ReportCategory.GIFTING, 
                $"[Prune] Pruning emotes. Pending: {pendingEmotes.Count}, Registry entries: {emoteRegistry.Count}");

            int pruned = PruneFromRegistry(pendingEmotes, emoteRegistry, "Emote");
            
            if (pruned > 0)
            {
                Save();
                ReportHub.Log(ReportCategory.GIFTING, $"[Prune] Pruned {pruned} emotes.");
            }
        }

        private static int PruneFromRegistry(
            Dictionary<string, PendingTransferEntry> pending,
            IReadOnlyDictionary<URN, Dictionary<URN, NftBlockchainOperationEntry>> registry,
            string itemType)
        {
            var toRemove = new List<string>();
            var now = DateTime.UtcNow;

            foreach (var (fullUrn, entry) in pending)
            {
                // Rule 1: Safety timeout - prune if pending for more than 1 hour
                double hoursPending = (now - entry.SentAtUtc).TotalHours;
                if (hoursPending >= TIMEOUT_HOURS)
                {
                    toRemove.Add(fullUrn);
                    ReportHub.Log(ReportCategory.GIFTING, 
                        $"[Prune] {itemType} pruned by timeout ({hoursPending:F1}h): {fullUrn}");
                    continue;
                }

                // Parse base URN
                if (!GiftingUrnParsingHelper.TryGetBaseUrn(fullUrn, out string baseUrnString))
                {
                    toRemove.Add(fullUrn);
                    ReportHub.Log(ReportCategory.GIFTING, $"[Prune] {itemType} invalid URN format, removing: {fullUrn}");
                    continue;
                }

                var baseUrn = new URN(baseUrnString);
                var fullUrnKey = new URN(fullUrn);

                // Rule 2: Check if base URN exists in registry
                if (!registry.TryGetValue(baseUrn, out var instances))
                {
                    // Base URN gone = user transferred their last copy of this item
                    toRemove.Add(fullUrn);
                    ReportHub.Log(ReportCategory.GIFTING, 
                        $"[Prune] {itemType} base URN gone from registry: {fullUrn}");
                    continue;
                }

                // Rule 3: Check if specific token exists in registry
                if (!TryFindInRegistry(instances, fullUrn, fullUrnKey, out var nftEntry))
                {
                    // Token gone = transfer confirmed
                    toRemove.Add(fullUrn);
                    ReportHub.Log(ReportCategory.GIFTING, 
                        $"[Prune] {itemType} token gone from registry: {fullUrn}");
                    continue;
                }

                // Rule 4: Token exists - check if it came back after we sent it (A→B→A scenario)
                if (nftEntry.TransferredAt > entry.SentAtUtc)
                {
                    toRemove.Add(fullUrn);
                    ReportHub.Log(ReportCategory.GIFTING, 
                        $"[Prune] {itemType} returned after transfer (registry: {nftEntry.TransferredAt:O}, sent: {entry.SentAtUtc:O}): {fullUrn}");
                    continue;
                }

                // Keep pending - transfer not yet confirmed by indexer
                ReportHub.Log(ReportCategory.GIFTING, 
                    $"[Prune] {itemType} still pending (sent {hoursPending:F2}h ago): {fullUrn}");
            }

            // Apply removals
            foreach (string urn in toRemove)
                pending.Remove(urn);

            return toRemove.Count;
        }

        private static bool TryFindInRegistry(
            IReadOnlyDictionary<URN, NftBlockchainOperationEntry> instances,
            string pendingUrn,
            URN fullUrnKey,
            out NftBlockchainOperationEntry entry)
        {
            // Primary: URN-based lookup (O(1))
            if (instances.TryGetValue(fullUrnKey, out entry))
                return true;

            // Fallback: Normalized string comparison (O(n) but handles case differences)
            string normalizedPending = pendingUrn.Trim().ToLowerInvariant();
            foreach (var kvp in instances)
            {
                string normalizedKey = kvp.Key.ToString().Trim().ToLowerInvariant();
                if (normalizedKey == normalizedPending)
                {
                    entry = kvp.Value;
                    ReportHub.Log(ReportCategory.GIFTING,
                        $"[Prune] Fallback match found! Registry key '{kvp.Key}' matches pending '{pendingUrn}'");
                    return true;
                }
            }

            entry = default;
            return false;
        }

        public void LogPendingTransfers()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Pending Transfers ===");
            
            sb.AppendLine($"Wearables ({pendingWearables.Count}):");
            foreach (var entry in pendingWearables.Values)
                sb.AppendLine($"  - {entry}");
            
            sb.AppendLine($"Emotes ({pendingEmotes.Count}):");
            foreach (var entry in pendingEmotes.Values)
                sb.AppendLine($"  - {entry}");
            
            ReportHub.Log(ReportCategory.GIFTING, sb.ToString());
        }

        private void Save()
        {
            persistence.SavePendingTransfers(pendingWearables.Values, pendingEmotes.Values);
        }
    }
}
