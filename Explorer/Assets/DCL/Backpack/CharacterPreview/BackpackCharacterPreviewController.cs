using Arch.Core;
using CommunicationData.URLHelpers;
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
            backpackEventBus.EquipEvent += OnEquipped;
            backpackEventBus.UnEquipEvent += OnUnequipped;
            backpackEventBus.FilterCategoryByEnumEvent += OnChangeCategory;
            backpackEventBus.ForceRenderEvent += OnForceRenderChange;
        }

        public new void Dispose()
        {
            base.Dispose();
            backpackEventBus.EquipEvent -= OnEquipped;
            backpackEventBus.UnEquipEvent -= OnUnequipped;
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

        private void OnEquipped(IWearable i)
        {
            previewAvatarModel.Wearables ??= new List<URN>();

            if (i.Type == WearableType.BodyShape)
                previewAvatarModel.BodyShape = i.GetUrn();
            else previewAvatarModel.Wearables.Add(i.GetUrn());

            OnModelUpdated();
        }

        private void OnUnequipped(IWearable i)
        {
            previewAvatarModel.Wearables.Remove(i.GetUrn());
            OnModelUpdated();
        }
    }
}
