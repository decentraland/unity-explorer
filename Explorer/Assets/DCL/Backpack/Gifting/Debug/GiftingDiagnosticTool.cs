using System.Collections.Generic;
using System.Text;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack.Gifting.Services.PendingTransfers;
using DCL.Diagnostics;
using DCL.Web3.Identities;
using UnityEngine;
using DCL.Backpack.Gifting.Utils;

namespace DCL.Backpack.Gifting.Debug
{
    public class GiftingDiagnosticTool : MonoBehaviour
    {
        private IPendingTransferService pendingTransferService;
        private IWearableStorage wearableStorage;
        private IEmoteStorage emoteStorage;
        private IWearablesProvider wearablesProvider;
        private IEmoteProvider emoteProvider;
        private IWeb3IdentityCache identityCache;

        public void Initialize(
            IPendingTransferService pendingTransferService,
            IWearableStorage wearableStorage,
            IEmoteStorage emoteStorage,
            IWearablesProvider wearablesProvider,
            IEmoteProvider emoteProvider,
            IWeb3IdentityCache identityCache)
        {
            this.pendingTransferService = pendingTransferService;
            this.wearableStorage = wearableStorage;
            this.emoteStorage = emoteStorage;
            this.wearablesProvider = wearablesProvider;
            this.emoteProvider = emoteProvider;
            this.identityCache = identityCache;
        }

        [ContextMenu("1. Print Pending Transfers")]
        public void PrintPendingTransfers()
        {
            pendingTransferService.LogPendingTransfers();
        }

        [ContextMenu("2. Print Owned Wearables (Registry)")]
        public void PrintWearableRegistry()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Wearable Registry Dump ===");
            
            // Accessing internal registry via interface might require casting or looking at exposed properties
            // Assuming standard IWearableStorage usage:
            foreach (var kvp in wearableStorage.AllOwnedNftRegistry)
            {
                string baseUrn = kvp.Key;
                var instances = kvp.Value;
                sb.AppendLine($"Base URN: {baseUrn} | Count: {instances.Count}");
                foreach (var instance in instances.Values)
                {
                    sb.AppendLine($"   - TokenId: {instance.TokenId} | Full URN: {instance.Urn}");
                }
            }
            ReportHub.Log(ReportCategory.GIFTING, sb.ToString());
        }

        [ContextMenu("3. Print Owned Emotes (Registry)")]
        public void PrintEmoteRegistry()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Emote Registry Dump ===");
            
            foreach (var kvp in emoteStorage.AllOwnedNftRegistry)
            {
                string baseUrn = kvp.Key;
                var instances = kvp.Value;
                sb.AppendLine($"Base URN: {baseUrn} | Count: {instances.Count}");
                foreach (var instance in instances.Values)
                {
                    sb.AppendLine($"   - TokenId: {instance.TokenId} | Full URN: {instance.Urn}");
                }
            }
            ReportHub.Log(ReportCategory.GIFTING, sb.ToString());
        }

        [ContextMenu("4. Fetch & Log Backpack Wearables (API)")]
        public void FetchBackpackWearables()
        {
            FetchWearablesAsync().Forget();
        }

        private async UniTaskVoid FetchWearablesAsync()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Fetching Backpack Wearables (Page 1) ===");
            
            var results = new List<ITrimmedWearable>();
            
            // Simulating Backpack Grid Request
            await wearablesProvider.GetAsync(
                pageSize: 20, 
                pageNumber: 1, 
                ct: System.Threading.CancellationToken.None,
                results: results
                // Add specific sorting/filtering if needed matches your backpack
            );

            foreach (var w in results)
            {
                sb.AppendLine($"[Wearable] Name: {w.GetName()} | URN: {w.GetUrn()} | Amount: {w.Amount}");
            }
            
            ReportHub.Log(ReportCategory.GIFTING, sb.ToString());
        }

        [ContextMenu("5. Fetch & Log Backpack Emotes (API)")]
        public void FetchBackpackEmotes()
        {
            FetchEmotesAsync().Forget();
        }

        private async UniTaskVoid FetchEmotesAsync()
        {
            var identity = identityCache.Identity;
            if (identity == null)
            {
                ReportHub.LogError(ReportCategory.GIFTING, "No identity found.");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("=== Fetching Backpack Emotes (Page 1) ===");

            var results = new List<IEmote>();
            
            // Simulating Backpack Emote Grid Request
            await emoteProvider.GetOwnedEmotesAsync(
                identity.Address,
                System.Threading.CancellationToken.None,
                new IEmoteProvider.OwnedEmotesRequestOptions(
                    pageNum: 1,
                    pageSize: 20,
                    collectionId: null,
                    orderOperation: new IEmoteProvider.OrderOperation("date", false),
                    name: ""
                ),
                results
            );

            foreach (var e in results)
            {
                bool isBase = GiftingUrnParsingHelper.TryGetBaseUrn(e.GetUrn(), out var baseUrn);
                sb.AppendLine($"[Emote] Name: {e.GetName()} | URN: {e.GetUrn()}");
                sb.AppendLine($"       -> IsOnChain: {e.IsOnChain()} | Parsed Base: {(isBase ? baseUrn : "N/A")}");
            }

            ReportHub.Log(ReportCategory.GIFTING, sb.ToString());
        }
        
        [ContextMenu("6. Simulate Prune Logic")]
        public void SimulatePrune()
        {
            ReportHub.Log(ReportCategory.GIFTING, "=== Simulating Prune ===");
            pendingTransferService.Prune(wearableStorage.AllOwnedNftRegistry, emoteStorage.AllOwnedNftRegistry);
            pendingTransferService.LogPendingTransfers();
        }
    }
}