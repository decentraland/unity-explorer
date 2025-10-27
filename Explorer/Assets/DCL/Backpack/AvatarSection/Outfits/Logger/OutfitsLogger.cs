using System.Collections.Generic;
using System.Linq;
using System.Text;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack.AvatarSection.Outfits.Models;
using DCL.Diagnostics;
using ECS;

namespace DCL.Backpack.AvatarSection.Outfits.Logger
{
    public class OutfitsLogger
    {
        private readonly IWearableStorage wearableStorage;
        private readonly IRealmData realmData;

        public OutfitsLogger(IWearableStorage wearableStorage, IRealmData realmData)
        {
            this.wearableStorage = wearableStorage;
            this.realmData = realmData;
        }

        public void LogEquippedState(string source, string? userId, IEquippedWearables equippedWearables, string? walletId = null)
        {
            var debugInfo = new StringBuilder();

            string userIdentifier = string.IsNullOrEmpty(walletId) ? $"User: {userId}" : $"User: {userId} | Wallet: {walletId}";
            debugInfo.AppendLine($"{source} DEBUG SNAPSHOT ({userIdentifier})");
            debugInfo.AppendLine($"RealmData state: {realmData}");

            foreach ((string category, var w) in equippedWearables.Items())
            {
                if (w == null) continue;

                var shortUrn = w.GetUrn();
                debugInfo.Append($"  - Category: {category,-15} | Short URN: '{shortUrn}'");

                if (wearableStorage.TryGetOwnedNftRegistry(shortUrn, out var registry) && registry.Count > 0)
                {
                    var fullUrn = registry.Values.First().Urn;
                    debugInfo.AppendLine($" -> [FOUND] Full URN: '{fullUrn}'");
                }
                else
                {
                    debugInfo.AppendLine(" -> [NOT FOUND] No full URN mapping exists in the registry yet!");
                }
            }

            ReportHub.Log(ReportCategory.OUTFITS, debugInfo.ToString());
        }

        public void LogLoadResult(IReadOnlyDictionary<int, OutfitItem> loadedOutfits)
        {
            if (loadedOutfits.Count == 0)
            {
                ReportHub.Log(ReportCategory.OUTFITS, "[OUTFIT_LOAD] No outfits loaded from server or data was empty.");
                return;
            }

            var debugInfo = new StringBuilder();
            debugInfo.AppendLine($"[OUTFIT_LOAD] Loaded {loadedOutfits.Count} valid outfits from server.");

            foreach (var kvp in loadedOutfits)
            {
                var outfitItem = kvp.Value;
                if (outfitItem.outfit == null) continue;

                debugInfo.AppendLine($"  -> Slot {outfitItem.slot}: {outfitItem.outfit.wearables.Count} wearables");
                foreach (string urn in outfitItem.outfit.wearables)
                    debugInfo.AppendLine($"     -> URN: '{urn}'");
            }

            ReportHub.Log(ReportCategory.OUTFITS, debugInfo.ToString());
        }

        public void LogInfo(string message)
        {
            ReportHub.Log(ReportCategory.OUTFITS, message);
        }

        public void LogError(string message)
        {
            ReportHub.LogError(ReportCategory.OUTFITS, message);
        }
    }
}