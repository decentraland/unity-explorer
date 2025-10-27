using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack.AvatarSection.Outfits.Models;
using DCL.Diagnostics;
using DCL.Profiles;
using DCL.Profiles.Self;
using ECS;
using Runtime.Wearables;

namespace DCL.Backpack.AvatarSection.Outfits.Commands
{
    public class PreviewOutfitCommand
    {
        private readonly OutfitApplier outfitApplier;
        private readonly IEquippedWearables equippedWearables;
        private readonly ISelfProfile selfProfile;
        private readonly IWearableStorage wearableStorage;
        private readonly IRealmData realmData;

        private Outfit? originalOutfit;

        public PreviewOutfitCommand(OutfitApplier outfitApplier,
            IEquippedWearables equippedWearables,
            ISelfProfile selfProfile,
            IWearableStorage wearableStorage,
            IRealmData realmData)
        {
            this.outfitApplier = outfitApplier;
            this.equippedWearables = equippedWearables;
            this.selfProfile = selfProfile;
            this.wearableStorage = wearableStorage;
            this.realmData = realmData;
        }

        public async UniTask ExecuteAsync(OutfitItem outfitToPreview, CancellationToken ct)
        {
            // If this is the first preview, store the current avatar state as the "original".
            if (originalOutfit == null)
                originalOutfit = await CreateOutfitFromEquippedAsync(ct);

            // Apply the previewed outfit
            if (outfitToPreview.outfit != null)
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

            equippedWearables.Items().TryGetValue(WearableCategories.Categories.BODY_SHAPE, out var bodyShapeWearable);

            var bodyShape = bodyShapeWearable?.GetUrn() ?? "";

            LogSystemState("[PreviewOutfitCommand - debug snapshot]", profile?.UserId, equippedWearables);
            
            return new Outfit
            {
                bodyShape = bodyShape, wearables = equippedWearables
                    .ToFullWearableUrns(wearableStorage, profile).Select(urn => urn.ToString()).ToList(),
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