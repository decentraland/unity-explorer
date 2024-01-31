using DCL.AvatarRendering.Wearables.Components;
using DCL.Backpack.BackpackBus;
using System.Collections.Generic;

namespace DCL.CharacterPreview
{
    public class BackpackCharacterPreviewController : CharacterPreviewControllerBase
    {
        private readonly BackpackEventBus backpackEventBus;

        public BackpackCharacterPreviewController(CharacterPreviewView view, ICharacterPreviewFactory previewFactory, BackpackEventBus backpackEventBus) : base(view, previewFactory)
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
            previewAvatarModel.ForceRender.Clear();

            foreach (string wearable in forceRender) { previewAvatarModel.ForceRender.Add(wearable); }

            OnModelUpdated();
        }

        private void OnEquipped(IWearable i)
        {
            previewAvatarModel.Wearables ??= new List<string>();

            if (i.IsBodyShape())
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
