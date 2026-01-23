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
                    string.Equals(extractedBase, baseUrn, StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                }
            }

            ReportHub.Log(ReportCategory.GIFTING, $"[PendingTransferService] GetPendingCount for {baseUrn}: {count}");
            return count;
        }

        /// <summary>
        /// Attempts to find a pending URN in the registry using primary URN lookup,
        /// with a fallback to normalized string comparison if the primary lookup fails.
        /// </summary>
        private bool TryFindInRegistry(
            IReadOnlyDictionary<URN, NftBlockchainOperationEntry> instances,
            string pendingUrn,
            URN fullUrnKey)
        {
            // Primary: URN-based lookup (O(1))
            if (instances.ContainsKey(fullUrnKey))
                return true;

            // Fallback: Normalized string comparison (O(n) but safe)
            // This handles cases where the URN format differs slightly between
            // what was stored in pending transfers and what the server returns
            string normalizedPending = pendingUrn.Trim().ToLowerInvariant();
            foreach (var key in instances.Keys)
            {
                string normalizedKey = key.ToString().Trim().ToLowerInvariant();
                if (normalizedKey == normalizedPending)
                {
                    ReportHub.Log(ReportCategory.GIFTING,
                        $"[Prune] Fallback match found! Registry key '{key}' matches pending '{pendingUrn}' via normalized string comparison");
                    return true;
                }
            }

            return false;
        }

        public void Prune(
            IReadOnlyDictionary<URN, Dictionary<URN, NftBlockchainOperationEntry>> wearableRegistry,
            IReadOnlyDictionary<URN, Dictionary<URN, NftBlockchainOperationEntry>> emoteRegistry)
        {
            if (pendingFullUrns.Count == 0) return;

            // We can only prune if at least one registry is loaded. 
            // If both are empty, we can't make any decisions (user might just be loading).
            if (wearableRegistry.Count == 0 && emoteRegistry.Count == 0) return;

            ReportHub.Log(ReportCategory.GIFTING,
                $"[Prune] Starting prune with {pendingFullUrns.Count} pending items. Registries loaded: Wearables={wearableRegistry.Count}, Emotes={emoteRegistry.Count}");

            var toRemove = new List<string>();

            foreach (string pendingUrn in pendingFullUrns)
            {
                if (!GiftingUrnParsingHelper.TryGetBaseUrn(pendingUrn, out string baseUrnString))
                {
                    ReportHub.Log(ReportCategory.GIFTING, $"[Prune] Invalid URN format, removing: {pendingUrn}");
                    toRemove.Add(pendingUrn);
                    continue;
                }

                var baseUrn = new URN(baseUrnString);
                var fullUrnKey = new URN(pendingUrn);

                if (wearableRegistry.ContainsKey(baseUrn))
                {
                    if (!TryFindInRegistry(wearableRegistry[baseUrn], pendingUrn, fullUrnKey))
                    {
                        toRemove.Add(pendingUrn);
                        ReportHub.Log(ReportCategory.GIFTING, $"[Prune] Wearable transfer confirmed (token gone): {pendingUrn}");
                    }

                    continue;
                }

                if (emoteRegistry.ContainsKey(baseUrn))
                {
                    // The Base URN exists in Emotes. This IS an emote.
                    if (!TryFindInRegistry(emoteRegistry[baseUrn], pendingUrn, fullUrnKey))
                    {
                        // Base exists, but Token is gone -> Transfer Confirmed.
                        toRemove.Add(pendingUrn);
                        ReportHub.Log(ReportCategory.GIFTING, $"[Prune] Emote transfer confirmed (token gone): {pendingUrn}");
                    }

                    // Else: Token still exists. Keep it.
                    continue;
                }

                bool wearablesLoaded = wearableRegistry.Count > 0;
                bool emotesLoaded = emoteRegistry.Count > 0;

                if (wearablesLoaded && emotesLoaded)
                {
                    // Both registries are active, and the item is in neither. 
                    // It means the user transferred their LAST copy of this item.
                    toRemove.Add(pendingUrn);
                    ReportHub.Log(ReportCategory.GIFTING, $"[Prune] Item no longer owned (base URN gone from both registries): {pendingUrn}");
                }
                else
                {
                    // One of the registries is empty. We can't be sure if this is a "Wearable awaiting WearableRegistry load"
                    // or an "Emote awaiting EmoteRegistry load". Safe bet: Keep it pending until data arrives.
                    ReportHub.Log(ReportCategory.GIFTING, $"[Prune] Item not found, but waiting for all registries to load to confirm removal: {pendingUrn}");
                }
            }

            // Apply removals
            if (toRemove.Count > 0)
            {
                foreach (string item in toRemove)
                    pendingFullUrns.Remove(item);

                persistence.SavePendingUrns(pendingFullUrns);
                ReportHub.Log(ReportCategory.GIFTING, $"[Prune] Pruned {toRemove.Count} confirmed gifts.");
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