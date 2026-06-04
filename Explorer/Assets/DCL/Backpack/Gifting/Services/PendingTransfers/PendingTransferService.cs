using System.Collections.Generic;
using System.Text;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack.Gifting.Models;
using DCL.Backpack.Gifting.Utils;
using DCL.Diagnostics;
using DCL.Web3.Identities;
using System;

namespace DCL.Backpack.Gifting.Services.PendingTransfers
{
    public class PendingTransferService : IPendingTransferService, IDisposable
    {
        private readonly IGiftingPersistence persistence;
        private readonly IWeb3IdentityCache identityCache;
        private readonly IWearableStorage wearableStorage;
        private readonly IEmoteStorage emoteStorage;

        // Maps a pending full URN to its gift-time baseline and kind. The baseline lets Prune tell "indexer
        // behind" from "left and came back" (newer timestamp); the kind scopes pruning to the fetched registry.
        // Guarded by a dedicated lock, not the dictionary itself, which is reassigned on identity change.
        private readonly object sync = new ();
        private Dictionary<string, PendingTransfer> pendingTransfers = new ();

        public PendingTransferService(IGiftingPersistence persistence,
            IWeb3IdentityCache identityCache,
            IWearableStorage wearableStorage,
            IEmoteStorage emoteStorage)
        {
            this.persistence = persistence;
            this.identityCache = identityCache;
            this.wearableStorage = wearableStorage;
            this.emoteStorage = emoteStorage;

            LoadPendingUrns();

            identityCache.OnIdentityChanged += LoadPendingUrns;
        }

        public void Dispose() =>
            identityCache.OnIdentityChanged -= LoadPendingUrns;

        private void LoadPendingUrns()
        {
            lock (sync)
            {
                pendingTransfers = persistence.LoadPendingUrns();

                ReportHub.Log(ReportCategory.GIFTING, $"[PendingTransferService] Loaded {pendingTransfers.Count} items from disk.");
                foreach (string? urn in pendingTransfers.Keys)
                    ReportHub.Log(ReportCategory.GIFTING, $"  - Loaded Pending: {urn}");
            }
        }

        public void AddPending(string fullUrn, DateTime baselineTransferredAt, GiftableType kind)
        {
            lock (sync)
            {
                if (pendingTransfers.ContainsKey(fullUrn))
                {
                    ReportHub.Log(ReportCategory.GIFTING, $"[PendingTransferService] Item already exists in pending: {fullUrn}");
                    return;
                }

                pendingTransfers[fullUrn] = new PendingTransfer(baselineTransferredAt, kind);
                ReportHub.Log(ReportCategory.GIFTING, $"[PendingTransferService] Adding new pending {kind}: {fullUrn} (baseline: {baselineTransferredAt:o})");
                persistence.SavePendingUrns(pendingTransfers);
            }
        }

        public bool IsPending(string fullUrn)
        {
            lock (sync)
                return pendingTransfers.ContainsKey(fullUrn);
        }

        // IsPending locks, keeping this consistent with concurrent main-thread mutation.
        public bool ShouldExclude(URN fullUrn) =>
            IsPending(fullUrn.ToString());

        public int GetPendingCount(string baseUrn)
        {
            int count = 0;
            lock (sync)
            {
                foreach (string pending in pendingTransfers.Keys)
                {
                    if (GiftingUrnParsingHelper.TryGetBaseUrn(pending, out string extractedBase) &&
                        extractedBase == baseUrn)
                    {
                        count++;
                    }
                }
            }

            ReportHub.Log(ReportCategory.GIFTING, $"[PendingTransferService] GetPendingCount for {baseUrn}: {count}");
            return count;
        }

        public void Prune(GiftableType kind)
        {
            IReadOnlyDictionary<URN, Dictionary<URN, NftBlockchainOperationEntry>> wearableRegistry = wearableStorage.AllOwnedNftRegistry;
            IReadOnlyDictionary<URN, Dictionary<URN, NftBlockchainOperationEntry>> emoteRegistry = emoteStorage.AllOwnedNftRegistry;
            IReadOnlyDictionary<URN, Dictionary<URN, NftBlockchainOperationEntry>> scopedRegistry =
                kind == GiftableType.Emote ? emoteRegistry : wearableRegistry;

            lock (sync)
            {
                if (pendingTransfers.Count == 0) return;

                var toRemove = new List<string>();

                foreach (KeyValuePair<string, PendingTransfer> pending in pendingTransfers)
                {
                    GiftableType? entryKind = pending.Value.Kind;

                    // A typed entry is pruned only by its own kind's call (the other registry may not be loaded,
                    // so its absence isn't conclusive). No-kind interim entries fall through and check both.
                    if (entryKind.HasValue && entryKind.Value != kind) continue;

                    string pendingUrn = pending.Key;
                    DateTime baseline = pending.Value.BaselineTransferredAt;

                    if (!GiftingUrnParsingHelper.TryGetBaseUrn(pendingUrn, out string baseUrnString))
                    {
                        toRemove.Add(pendingUrn);
                        continue;
                    }

                    var baseUrn = new URN(baseUrnString);
                    var fullUrnKey = new URN(pendingUrn);

                    NftBlockchainOperationEntry entry = default;
                    bool found = entryKind.HasValue
                        ? scopedRegistry.TryGetValue(baseUrn, out var instances) && instances.TryGetValue(fullUrnKey, out entry)
                        : (wearableRegistry.TryGetValue(baseUrn, out var wInstances) && wInstances.TryGetValue(fullUrnKey, out entry)) ||
                          (emoteRegistry.TryGetValue(baseUrn, out var eInstances) && eInstances.TryGetValue(fullUrnKey, out entry));

                    // Not owned anymore: indexer caught up, item left the wallet. Stop tracking.
                    if (!found)
                    {
                        toRemove.Add(pendingUrn);
                        continue;
                    }

                    // Still owned but with a newer transfer-in than gift time: it left and came back (e.g. gifted
                    // back). The original gift completed, so stop tracking; else the copy stays hidden forever.
                    // Baseline-less legacy entries are dropped on load, so this comparison is always valid here.
                    if (entry.TransferredAt > baseline)
                        toRemove.Add(pendingUrn);
                }

                if (toRemove.Count > 0)
                {
                    foreach (string item in toRemove)
                        pendingTransfers.Remove(item);

                    persistence.SavePendingUrns(pendingTransfers);
                    ReportHub.Log(ReportCategory.GIFTING, $"Pruned {toRemove.Count} confirmed {kind} gifts.");
                }
            }
        }

        public void LogPendingTransfers()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Pending Transfers:");
            lock (sync)
            {
                foreach (string? urn in pendingTransfers.Keys) sb.AppendLine(urn);
            }
            ReportHub.Log(ReportCategory.GIFTING, sb.ToString());
        }
    }
}
