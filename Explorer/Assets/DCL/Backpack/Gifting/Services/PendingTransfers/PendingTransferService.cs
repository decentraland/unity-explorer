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

        // Maps a pending full URN (token instance) to its gift-time baseline and kind. The baseline lets Prune
        // distinguish "indexer hasn't caught up yet" from "the item left the wallet and was transferred back"
        // (a newer transfer-in timestamp); the kind keeps pruning scoped to the registry that was just fetched.
        // Reassigned wholesale on identity change, so all access is guarded by the dedicated lock below rather
        // than by locking the dictionary instance itself (which would change out from under a held lock).
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

        // IsPending takes the lock, keeping this read consistent with concurrent mutation from the main thread.
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

                    // A typed entry is only evaluated by its own kind's Prune call, against the registry that was
                    // just fetched (the other kind's registry may not be loaded yet, so its absence is not
                    // conclusive). Legacy entries have no kind, so they fall through and are checked against both.
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

                    // Not owned anymore: the Indexer caught up and the item left the wallet. Stop tracking it.
                    if (!found)
                    {
                        toRemove.Add(pendingUrn);
                        continue;
                    }

                    // Still owned, but with a newer transfer-in timestamp than the one captured at gift time:
                    // the item left the wallet and was transferred back (e.g. gifted back). The original gift
                    // completed, so stop tracking it; otherwise the copy would stay hidden forever.
                    // Every persisted entry carries a real baseline (baseline-less legacy entries are dropped on
                    // load), so this comparison is always meaningful here.
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
