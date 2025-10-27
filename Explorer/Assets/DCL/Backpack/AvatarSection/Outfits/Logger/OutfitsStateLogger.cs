using System.Linq;
using System.Text;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using ECS;

namespace DCL.Backpack.AvatarSection.Outfits.Logger
{
    public class OutfitsStateLogger
    {
        private readonly IWearableStorage wearableStorage;
        private readonly IRealmData realmData;

        public OutfitsStateLogger(IWearableStorage wearableStorage, IRealmData realmData)
        {
            this.wearableStorage = wearableStorage;
            this.realmData = realmData;
        }

        public void Log(string source, string? userId, IEquippedWearables equippedWearables, string? walletId = null)
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
    }
}