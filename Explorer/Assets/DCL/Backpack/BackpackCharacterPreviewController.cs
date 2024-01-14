using Arch.Core;
using Arch.SystemGroups;
using DCL.AvatarRendering.Wearables.Components;
using DCL.Backpack.BackpackBus;
using UnityEngine;

namespace DCL.CharacterPreview
{
    public class BackpackCharacterPreviewController
    {
        private readonly BackpackCharacterPreviewView view;
        private readonly ICharacterPreviewFactory previewFactory;
        private readonly BackpackEventBus eventBus;

        private World world;
        private CharacterPreviewController previewController;
        private CharacterPreviewModel previewModel;

        public BackpackCharacterPreviewController(BackpackCharacterPreviewView view, ICharacterPreviewFactory previewFactory, BackpackEventBus eventBus)
        {
            this.view = view;
            this.previewFactory = previewFactory;
            this.eventBus = eventBus;
            this.eventBus.EquipEvent +=  OnEquipped;
            this.eventBus.UnEquipEvent += OnUnequipped;

            // Subscribe to the event bus
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in Entity playerEntity)
        {
            world = builder.World;
        }


        public void OnShow()
        {
            return;
            view.Initialize();
            previewController = previewFactory.Create(world, view.CharacterPreviewContainer);
            previewController.UpdateAvatar(previewModel);
        }

        public void OnHide()
        {
        }

        private void OnEquipped(IWearable i)
        {
            // Change model
            UpdateModel();
        }

        private void OnUnequipped(IWearable i)
        {

            // Change model
            UpdateModel();
        }

        private void UpdateModel()
        {
            previewController.UpdateAvatar(previewModel);
        }

        public void Dispose()
        {
            this.eventBus.EquipEvent -= OnEquipped;
            this.eventBus.UnEquipEvent -= OnUnequipped;

        }
    }
}
