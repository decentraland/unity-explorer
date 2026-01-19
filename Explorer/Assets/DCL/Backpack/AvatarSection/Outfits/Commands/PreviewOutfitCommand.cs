using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack.AvatarSection.Outfits.Logger;
using DCL.Backpack.AvatarSection.Outfits.Models;
using DCL.Profiles;
using DCL.Profiles.Self;
using Runtime.Wearables;

namespace DCL.Backpack.AvatarSection.Outfits.Commands
{
    public class PreviewOutfitCommand
    {
        private readonly OutfitApplier outfitApplier;
        private readonly IEquippedWearables equippedWearables;
        private readonly ISelfProfile selfProfile;
        private readonly IWearableStorage wearableStorage;
        private readonly OutfitsLogger outfitsLogger;

        private Outfit? originalOutfit;
        private List<IWearable>? originalWearables;

        public PreviewOutfitCommand(OutfitApplier outfitApplier,
            IEquippedWearables equippedWearables,
            ISelfProfile selfProfile,
            IWearableStorage wearableStorage,
            OutfitsLogger outfitsLogger)
        {
            this.outfitApplier = outfitApplier;
            this.equippedWearables = equippedWearables;
            this.selfProfile = selfProfile;
            this.wearableStorage = wearableStorage;
            this.outfitsLogger = outfitsLogger;
        }

        public async UniTask ExecuteAsync(OutfitItem outfitToPreview, CancellationToken ct)
        {
            // If this is the first preview, store the current avatar state as the "original".
            if (originalOutfit == null)
            {
                // Capture raw data for sync restore
                CaptureOriginalDataState();
                
                // Capture DTO for visual restore
                originalOutfit = await CreateOutfitFromEquippedAsync(ct);
            }

            // Apply the previewed outfit
            if (outfitToPreview.outfit != null)
                outfitApplier.Apply(outfitToPreview.outfit);
        }

        // Restores the original outfit if one was stored
        public void Restore()
        {
            if (originalOutfit != null)
            {
                // 1. Synchronously restore the data source (EquippedWearables).
                // This ensures that if 'PublishProfile' happens immediately after this call,
                // it sees the correct (original) data, avoiding race conditions.
                RestoreDataStateSynchronously();

                // 2. Trigger the visual update (Async via Event Bus).
                // This updates the 3D preview and UI to match the data we just restored.
                outfitApplier.Apply(originalOutfit);
            }

            // Clear the stored state
            originalOutfit = null;
            originalWearables = null;
        }

        // Called when a user permanently equips an outfit, invalidating the "original"
        public void Commit()
        {
            originalOutfit = null;
            originalWearables = null;
        }

        private void CaptureOriginalDataState()
        {
            originalWearables = new List<IWearable>();
            foreach (var kvp in equippedWearables.Items())
            {
                if (kvp.Value != null)
                    originalWearables.Add(kvp.Value);
            }
        }

        private void RestoreDataStateSynchronously()
        {
            if (originalWearables == null || originalOutfit == null) return;

            equippedWearables.UnEquipAll();

            foreach (var w in originalWearables)
                equippedWearables.Equip(w);

            equippedWearables.SetEyesColor(originalOutfit.eyes.color);
            equippedWearables.SetHairColor(originalOutfit.hair.color);
            equippedWearables.SetBodyshapeColor(originalOutfit.skin.color);
            equippedWearables.SetForceRender(originalOutfit.forceRender);
        }

        private async UniTask<Outfit> CreateOutfitFromEquippedAsync(CancellationToken ct)
        {
            var profile = await selfProfile.ProfileAsync(ct);
            
            var (hair, eyes, skin) = equippedWearables.GetColors();

            equippedWearables.Items().TryGetValue(WearableCategories.Categories.BODY_SHAPE, out var bodyShapeWearable);

            var bodyShape = bodyShapeWearable?.GetUrn() ?? "";

            outfitsLogger.LogEquippedState("[PreviewOutfitCommand - outfit state]", profile?.UserId, equippedWearables);
            
            return new Outfit
            {
                bodyShape = bodyShape, wearables = equippedWearables
                    .ToFullWearableUrns(wearableStorage, profile),
                forceRender = new List<string>(equippedWearables.ForceRenderCategories),
                hair = new Hair
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