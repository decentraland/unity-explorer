using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack.AvatarSection.Outfits.Models;
using DCL.Backpack.AvatarSection.Outfits.Repository;
using DCL.Backpack.Outfits.Extensions;
using DCL.Diagnostics;
using DCL.Profiles.Self;
using Runtime.Wearables;
using UnityEngine;

namespace DCL.Backpack.AvatarSection.Outfits.Commands
{
    public class SaveOutfitCommand
    {
        private readonly ISelfProfile selfProfile;
        private readonly OutfitsRepository outfitsRepository;
        private readonly IWearableStorage wearableStorage;

        public SaveOutfitCommand(ISelfProfile selfProfile, OutfitsRepository outfitsRepository, IWearableStorage wearableStorage)
        {
            this.selfProfile = selfProfile;
            this.outfitsRepository = outfitsRepository;
            this.wearableStorage = wearableStorage;
        }

        public async UniTask<OutfitItem> ExecuteAsync(int slotIndex,
            IEquippedWearables equippedWearables,
            IReadOnlyCollection<OutfitItem> currentOutfits, CancellationToken ct)
        {
            var profile = await selfProfile.ProfileAsync(ct);
            
            if (profile == null)
                throw new InvalidOperationException("Cannot save outfit, self profile is not loaded.");

            var fullWearableUrns = equippedWearables.ToFullWearableUrns(wearableStorage, profile);
            
            ReportHub.Log(ReportCategory.OUTFITS, $"[OUTFIT_SAVE] Saving Outfit in Slot {slotIndex}. Contains {fullWearableUrns.Count} wearables.");
            
            foreach (string? urn in fullWearableUrns)
                ReportHub.Log(ReportCategory.OUTFITS, $"[OUTFIT_SAVE]   -> Wearable URN: '{urn}'");

            var (hairColor, eyesColor, skinColor) = equippedWearables.GetColors();
            
            if (!equippedWearables.Items().TryGetValue(WearableCategories.Categories.BODY_SHAPE, out var bodyShapeWearable) || bodyShapeWearable == null)
                throw new InvalidOperationException("Cannot save outfit, Body Shape is not equipped!");

            var newItem = new OutfitItem
            {
                slot = slotIndex, outfit = new Outfit
                {
                    bodyShape = bodyShapeWearable.GetUrn(), wearables = fullWearableUrns, forceRender = new List<string>(), eyes = new Eyes
                    {
                        color = eyesColor
                    },
                    hair = new Hair
                    {
                        color = hairColor
                    },
                    skin = new Skin
                    {
                        color = skinColor
                    }
                }
            };

            var updatedOutfits = currentOutfits.ToList();
            int existingIndex = updatedOutfits.FindIndex(o => o.slot == slotIndex);

            // NOTE: update or add existing outfit
            if (existingIndex != -1) updatedOutfits[existingIndex] = newItem;
            else updatedOutfits.Add(newItem);
            
            await outfitsRepository.SetAsync(profile, updatedOutfits, ct);
            return newItem;
        }

        private OutfitItem CreateEmptyOutfitItem(int slot)
        {
            return new OutfitItem
            {
                slot = slot, outfit = new Outfit
                {
                    bodyShape = "", eyes = new Eyes
                    {
                        color = new Color(0, 0, 0, 0)
                    },
                    hair = new Hair
                    {
                        color = new Color(0, 0, 0, 0)
                    },
                    skin = new Skin
                    {
                        color = new Color(0, 0, 0, 0)
                    },
                    wearables = new List<string>(), forceRender = new List<string>()
                }
            };
        }
    }
}