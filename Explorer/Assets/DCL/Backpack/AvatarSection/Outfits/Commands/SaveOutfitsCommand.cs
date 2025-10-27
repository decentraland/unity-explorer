using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack.AvatarSection.Outfits.Events;
using DCL.Backpack.AvatarSection.Outfits.Models;
using DCL.Backpack.AvatarSection.Outfits.Repository;
using DCL.Diagnostics;
using DCL.Profiles;
using DCL.Profiles.Self;
using ECS;
using Runtime.Wearables;
using Utility;


namespace DCL.Backpack.AvatarSection.Outfits.Commands
{
    public class SaveOutfitCommand
    {
        private readonly ISelfProfile selfProfile;
        private readonly OutfitsRepository outfitsRepository;
        private readonly IWearableStorage wearableStorage;
        private readonly IRealmData realmData;
        private readonly IEventBus eventBus;

        public SaveOutfitCommand(ISelfProfile selfProfile,
            OutfitsRepository outfitsRepository,
            IWearableStorage wearableStorage,
            IRealmData realmData,
            IEventBus eventBus)
        {
            this.selfProfile = selfProfile;
            this.outfitsRepository = outfitsRepository;
            this.wearableStorage = wearableStorage;
            this.realmData = realmData;
            this.eventBus = eventBus;
        }

        public async UniTask<OutfitItem> ExecuteAsync(int slotIndex,
            IEquippedWearables equippedWearables,
            IReadOnlyCollection<OutfitItem> currentOutfits, CancellationToken ct)
        {
            var profile = await selfProfile.ProfileAsync(ct);
            
            if (profile == null)
                throw new InvalidOperationException("Cannot save outfit, self profile is not loaded.");

            LogSystemState("[SaveOutfitCommand - debug snapshot]", profile.UserId, equippedWearables);
            
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
                    bodyShape = bodyShapeWearable.GetUrn(), wearables = fullWearableUrns, forceRender = new List<string>(equippedWearables.ForceRenderCategories), eyes = new Eyes
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

            var shortUrns = equippedWearables.ToShortWearableUrns();
            eventBus.Publish(new OutfitsEvents.SaveOutfitEvent(shortUrns));
            return newItem;
        }

        private void LogSystemState(string source, string? userId, IEquippedWearables equippedWearables)
        {
            var debugInfo = new StringBuilder();

            debugInfo.AppendLine($"----------- {source} DEBUG SNAPSHOT (User: {userId}) -----------");
            debugInfo.AppendLine($"[SaveOutfit] Profile state {userId}");
            debugInfo.AppendLine($"RealmData state {userId} {realmData}");

            foreach ((string category, var w) in equippedWearables.Items())
            {
                if (w == null) continue;

                var shortUrn = w.GetUrn();
                debugInfo.Append($"  - Cat: {category,-15} | Short URN: '{shortUrn}'");
                if (wearableStorage.TryGetOwnedNftRegistry(shortUrn, out var registry) && registry.Count > 0)
                {
                    var fullUrn = registry.First().Value.Urn;
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