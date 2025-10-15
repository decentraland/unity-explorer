using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack.AvatarSection.Outfits.Models;
using DCL.Backpack.Outfits.Extensions;
using DCL.Profiles.Self;
using Runtime.Wearables;

namespace DCL.Backpack.AvatarSection.Outfits.Commands
{
    public class PreviewOutfitCommand
    {
        private readonly IOutfitApplier outfitApplier;
        private readonly IEquippedWearables equippedWearables;
        private readonly ISelfProfile selfProfile;
        private readonly IWearableStorage wearableStorage;

        private Outfit? originalOutfit; // State: Stores the original outfit

        public PreviewOutfitCommand(IOutfitApplier outfitApplier, IEquippedWearables equippedWearables, ISelfProfile selfProfile, IWearableStorage wearableStorage)
        {
            this.outfitApplier = outfitApplier;
            this.equippedWearables = equippedWearables;
            this.selfProfile = selfProfile;
            this.wearableStorage = wearableStorage;
        }

        public async UniTask ExecuteAsync(OutfitItem outfitToPreview, CancellationToken ct)
        {
            // If this is the first preview, store the current avatar state as the "original".
            if (originalOutfit == null)
                originalOutfit = await CreateOutfitFromEquippedAsync(ct);

            // Apply the previewed outfit
            outfitApplier.Apply(outfitToPreview.outfit);
        }

        // Restores the original outfit if one was stored
        public void Restore()
        {
            if (originalOutfit != null)
                outfitApplier.Apply(originalOutfit);

            // Clear the stored state
            originalOutfit = null;
        }

        // Called when a user permanently equips an outfit, invalidating the "original"
        public void Commit()
        {
            originalOutfit = null;
        }

        private async UniTask<Outfit> CreateOutfitFromEquippedAsync(CancellationToken ct)
        {
            var profile = await selfProfile.ProfileAsync(ct);
            var (hair, eyes, skin) = equippedWearables.GetColors();
            var bodyShape = equippedWearables.Items().FirstOrDefault(i => i.Key == WearableCategories.Categories.BODY_SHAPE).Value?.GetUrn() ?? "";

            return new Outfit
            {
                bodyShape = bodyShape, wearables = equippedWearables.ToFullWearableUrns(wearableStorage, profile).ToArray(), hair = new Hair
                {
                    color = hair
                },
                eyes = new Eyes
                {
                    color = eyes
                },
                skin = new Skin
                {
                    color = skin
                }
            };
        }
    }
}