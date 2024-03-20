using Arch.Core;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Wearables.Components;
using DCL.Backpack.BackpackBus;
using DCL.CharacterPreview;
using System.Collections.Generic;

namespace DCL.Backpack.CharacterPreview
{
    public class BackpackCharacterPreviewController : CharacterPreviewControllerBase
    {
        private readonly BackpackEventBus backpackEventBus;

        public BackpackCharacterPreviewController(CharacterPreviewView view, ICharacterPreviewFactory previewFactory, BackpackEventBus backpackEventBus, World world)
            : base(view, previewFactory, world)
        {
            this.backpackEventBus = backpackEventBus;
            backpackEventBus.EquipWearableEvent += OnWearableEquipped;
            backpackEventBus.UnEquipWearableEvent += OnWearableUnequipped;
            backpackEventBus.EquipEmoteEvent += OnEmoteEquipped;
            backpackEventBus.UnEquipEmoteEvent += OnEmoteUnEquipped;
            backpackEventBus.FilterCategoryByEnumEvent += OnChangeCategory;
            backpackEventBus.ForceRenderEvent += OnForceRenderChange;
        }

        public new void Dispose()
        {
            base.Dispose();
            backpackEventBus.EquipWearableEvent -= OnWearableEquipped;
            backpackEventBus.UnEquipWearableEvent -= OnWearableUnequipped;
        }

        private void OnChangeCategory(AvatarWearableCategoryEnum categoryEnum)
        {
            inputEventBus.OnChangePreviewFocus(categoryEnum);
        }

        private void OnForceRenderChange(IReadOnlyCollection<string> forceRender)
        {
            previewAvatarModel.ForceRenderCategories.Clear();

            foreach (string wearable in forceRender) { previewAvatarModel.ForceRenderCategories.Add(wearable); }

            OnModelUpdated();
        }

        private void OnWearableEquipped(IWearable i)
        {
            previewAvatarModel.Wearables ??= new List<URN>();

            if (i.IsBodyShape())
                previewAvatarModel.BodyShape = i.GetUrn();
            else previewAvatarModel.Wearables.Add(i.GetUrn());

            OnModelUpdated();
        }

        private void OnWearableUnequipped(IWearable i)
        {
            previewAvatarModel.Wearables.Remove(i.GetUrn());
            OnModelUpdated();
        }

        private void OnEmoteUnEquipped(int slot, IEmote? emote)
        {
            if (emote == null) return;
            if (previewAvatarModel.Emotes == null) return;
            previewAvatarModel.Emotes.Remove(emote.GetUrn());
            OnModelUpdated();
        }

        private void OnEmoteEquipped(int slot, IEmote emote)
        {
            previewAvatarModel.Emotes ??= new HashSet<URN>();

            URN urn = emote.GetUrn().Shorten();
            if (previewAvatarModel.Emotes.Contains(urn)) return;
            previewAvatarModel.Emotes.Add(urn);
            OnModelUpdated();
        }
    }
}
