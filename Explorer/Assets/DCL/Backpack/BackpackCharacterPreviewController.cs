using Arch.Core;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables.Components;
using DCL.Backpack.BackpackBus;
using DCL.Profiles;
using System.Collections.Generic;

namespace DCL.CharacterPreview
{
    public class BackpackCharacterPreviewController
    {
        private readonly BackpackCharacterPreviewView view;
        private readonly ICharacterPreviewFactory previewFactory;
        private readonly BackpackEventBus eventBus;

        private World world;
        private Entity playerEntity;
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
            this.playerEntity = playerEntity;
        }


        public void OnShow()
        {
            view.Initialize();
            previewController = previewFactory.Create(world, view.CharacterPreviewContainer);
            UpdateModel();
        }

        public void OnHide()
        {
        }

        private void OnEquipped(IWearable i)
        {
            // Change model
            previewModel.Wearables ??= new List<string>();
            previewModel.Wearables.Add(i.GetHash());
            UpdateModel();
        }

        private void OnUnequipped(IWearable i)
        {

            // Change model
            previewModel.Wearables.Remove(i.GetHash());
            UpdateModel();
        }

        private void UpdateModel()
        {
            var avatar = world.Get<Profile>(playerEntity).Avatar;
            previewModel.BodyShape = avatar.BodyShape;
            previewModel.HairColor = avatar.HairColor;
            previewModel.SkinColor = avatar.SkinColor;
            previewController.UpdateAvatar(previewModel);
        }

        public void Dispose()
        {
            this.eventBus.EquipEvent -= OnEquipped;
            this.eventBus.UnEquipEvent -= OnUnequipped;
        }
    }
}
