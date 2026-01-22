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

        // public void Prune(
        //     IReadOnlyDictionary<URN, Dictionary<URN, NftBlockchainOperationEntry>> wearableRegistry,
        //     IReadOnlyDictionary<URN, Dictionary<URN, NftBlockchainOperationEntry>> emoteRegistry)
        // {
        //     if (pendingFullUrns.Count == 0) return;
        //
        //     // Skip if BOTH registries are empty - we can't validate anything
        //     if (wearableRegistry.Count == 0 &&
        //         emoteRegistry.Count == 0) return;
        //
        //     ReportHub.Log(ReportCategory.GIFTING,
        //         $"[Prune] Starting prune with {pendingFullUrns.Count} pending items, {wearableRegistry.Count} wearable types, {emoteRegistry.Count} emote types");
        //
        //     var toRemove = new List<string>();
        //
        //     foreach (string pendingUrn in pendingFullUrns)
        //     {
        //         if (!GiftingUrnParsingHelper.TryGetBaseUrn(pendingUrn, out string baseUrnString))
        //         {
        //             ReportHub.Log(ReportCategory.GIFTING, $"[Prune] Invalid URN format, removing: {pendingUrn}");
        //             toRemove.Add(pendingUrn);
        //             continue;
        //         }
        //
        //         var baseUrn = new URN(baseUrnString);
        //         var fullUrnKey = new URN(pendingUrn);
        //
        //         bool stillOwned = false;
        //         bool foundInAnyRegistry = false;
        //
        //         // Check wearables - but only if wearable registry is populated
        //         if (wearableRegistry.Count > 0)
        //         {
        //             if (wearableRegistry.TryGetValue(baseUrn, out var wInstances))
        //             {
        //                 foundInAnyRegistry = true;
        //                 ReportHub.Log(ReportCategory.GIFTING,
        //                     $"[Prune] Found base URN '{baseUrnString}' in wearable registry with {wInstances.Count} instances");
        //
        //                 stillOwned = TryFindInRegistry(wInstances, pendingUrn, fullUrnKey);
        //
        //                 if (!stillOwned)
        //                 {
        //                     // Log diagnostic info about what keys ARE in the registry
        //                     foreach (var key in wInstances.Keys)
        //                     {
        //                         ReportHub.Log(ReportCategory.GIFTING,
        //                             $"[Prune] Registry has: '{key}' | Pending: '{pendingUrn}' | URN Match: {key.Equals(fullUrnKey)}");
        //                     }
        //                 }
        //             }
        //         }
        //
        //         // Check emotes - but only if emote registry is populated and not found in wearables
        //         if (!stillOwned && emoteRegistry.Count > 0)
        //         {
        //             if (emoteRegistry.TryGetValue(baseUrn, out var eInstances))
        //             {
        //                 foundInAnyRegistry = true;
        //                 ReportHub.Log(ReportCategory.GIFTING,
        //                     $"[Prune] Found base URN '{baseUrnString}' in emote registry with {eInstances.Count} instances");
        //
        //                 stillOwned = TryFindInRegistry(eInstances, pendingUrn, fullUrnKey);
        //
        //                 if (!stillOwned)
        //                 {
        //                     // Log diagnostic info about what keys ARE in the registry
        //                     foreach (var key in eInstances.Keys)
        //                     {
        //                         ReportHub.Log(ReportCategory.GIFTING,
        //                             $"[Prune] Registry has: '{key}' | Pending: '{pendingUrn}' | URN Match: {key.Equals(fullUrnKey)}");
        //                     }
        //                 }
        //             }
        //         }
        //
        //         // Only remove if:
        //         // 1. Item was found in a registry but no longer has the specific token (transfer confirmed)
        //         // 2. OR both registries are populated and the base URN is not found in either
        //         // Do NOT remove if either registry is empty (we can't distinguish wearable vs emote URNs)
        //         if (!stillOwned && foundInAnyRegistry)
        //         {
        //             // Item's base URN was found but specific token is gone - transfer confirmed
        //             ReportHub.Log(ReportCategory.GIFTING,
        //                 $"[Prune] Item no longer owned (base URN found, token gone), removing from pending: {pendingUrn}");
        //             toRemove.Add(pendingUrn);
        //         }
        //         else if (!stillOwned && !foundInAnyRegistry)
        //         {
        //             // Base URN not found in any populated registry
        //             // Since wearable and emote URNs look identical (both use collections-v2),
        //             // we can only safely prune when BOTH registries are populated
        //             bool bothRegistriesLoaded = wearableRegistry.Count > 0 && emoteRegistry.Count > 0;
        //             
        //             if (bothRegistriesLoaded)
        //             {
        //                 ReportHub.Log(ReportCategory.GIFTING,
        //                     $"[Prune] Item no longer owned (base URN not in any registry), removing from pending: {pendingUrn}");
        //                 toRemove.Add(pendingUrn);
        //             }
        //             else
        //             {
        //                 ReportHub.Log(ReportCategory.GIFTING,
        //                     $"[Prune] Skipping item - not all registries loaded yet: {pendingUrn} (wearables: {wearableRegistry.Count}, emotes: {emoteRegistry.Count})");
        //             }
        //         }
        //         else
        //         {
        //             ReportHub.Log(ReportCategory.GIFTING,
        //                 $"[Prune] Item still owned, keeping in pending: {pendingUrn}");
        //         }
        //     }
        //
        //     if (toRemove.Count > 0)
        //     {
        //         foreach (string item in toRemove)
        //             pendingFullUrns.Remove(item);
        //
        //         persistence.SavePendingUrns(pendingFullUrns);
        //         ReportHub.Log(ReportCategory.GIFTING, $"[Prune] Pruned {toRemove.Count} confirmed gifts.");
        //     }
        //     else
        //     {
        //         ReportHub.Log(ReportCategory.GIFTING, $"[Prune] No items to prune.");
        //     }
        // }

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

                // --- Logic Branch 1: Is it a Wearable? ---
                if (wearableRegistry.ContainsKey(baseUrn))
                {
                    // The Base URN exists in Wearables. This IS a wearable.
                    // Check if the specific Token (pendingUrn) is still there.
                    if (!TryFindInRegistry(wearableRegistry[baseUrn], pendingUrn, fullUrnKey))
                    {
                        // Base exists, but Token is gone -> Transfer Confirmed.
                        toRemove.Add(pendingUrn);
                        ReportHub.Log(ReportCategory.GIFTING, $"[Prune] Wearable transfer confirmed (token gone): {pendingUrn}");
                    }

                    // Else: Token still exists -> Transfer still pending. Keep it.
                    continue;
                }

                // --- Logic Branch 2: Is it an Emote? ---
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

                // --- Logic Branch 3: It's in neither registry ---
                // This implies the user owns *zero* copies of this item type anymore 
                // (the whole Base URN entry is gone), OR the registries aren't fully loaded.

                // Heuristic: 
                // If Wearable Registry is populated, but this item isn't in it, AND
                // If Emote Registry is populated, but this item isn't in it...
                // Then the user definitely doesn't own it.

                // However, we need to be careful not to prune a Wearable just because Emote registry is loaded but Wearable isn't.

                // Since we can't easily distinguish a Wearable URN from an Emote URN just by string (both are collections-v2),
                // we generally have to wait for BOTH to be loaded to safely prune "Orphans" (items not found in either).

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