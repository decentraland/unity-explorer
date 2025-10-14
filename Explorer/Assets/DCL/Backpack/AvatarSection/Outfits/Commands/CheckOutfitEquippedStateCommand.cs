using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack.AvatarSection.Outfits.Models;
using DCL.Backpack.Outfits.Extensions;
using DCL.Profiles.Self;

namespace DCL.Backpack.AvatarSection.Outfits.Commands
{
    public class CheckOutfitEquippedStateCommand
    {
        private readonly ISelfProfile selfProfile;
        private readonly IWearableStorage wearableStorage;

        public CheckOutfitEquippedStateCommand(ISelfProfile selfProfile, IWearableStorage wearableStorage)
        {
            this.selfProfile = selfProfile;
            this.wearableStorage = wearableStorage;
        }

        public async UniTask<bool> ExecuteAsync(OutfitItem savedOutfit, IEquippedWearables equippedWearables, CancellationToken ct)
        {
            if (savedOutfit.outfit == null) return false;

            var (hairColor, eyesColor, skinColor) = equippedWearables.GetColors();

            if (savedOutfit.outfit.hair.color != hairColor ||
                savedOutfit.outfit.eyes.color != eyesColor ||
                savedOutfit.outfit.skin.color != skinColor)
            {
                return false;
            }

            var profile = await selfProfile.ProfileAsync(ct);
            if (profile == null) return false;

            var liveWearableUrns = new HashSet<string>(
                equippedWearables.ToFullWearableUrns(wearableStorage, profile)
            );

            var savedWearableUrns = new HashSet<string>(savedOutfit.outfit.wearables);

            return liveWearableUrns.SetEquals(savedWearableUrns);
        }
    }
}