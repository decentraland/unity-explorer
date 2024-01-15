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
            this.eventBus.EquipEvent += OnEquipped;
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

        public void OnHide() { }

        private void OnEquipped(IWearable i)
        {
            // Change model
            previewModel.Wearables ??= new List<string>();

            if (!previewModel.Wearables.Contains(i.GetUrn()))
            {
                previewModel.Wearables.Add(i.GetUrn());
            }
            UpdateModel();
        }

        private void OnUnequipped(IWearable i)
        {
            // Change model
            previewModel.Wearables.Remove(i.GetUrn());
            UpdateModel();
        }

        private void UpdateModel()
        {
            Avatar avatar = world.Get<Profile>(playerEntity).Avatar;
            previewModel.Wearables ??= new List<string>();

            foreach (URN avatarSharedWearable in world.Get<Profile>(playerEntity).Avatar.SharedWearables)
            {
                if (!previewModel.Wearables.Contains(avatarSharedWearable))
                {
                    previewModel.Wearables.Add(avatarSharedWearable);
                }
            }

            previewModel.BodyShape = avatar.BodyShape;
            previewModel.HairColor = avatar.HairColor;
            previewModel.SkinColor = avatar.SkinColor;
            previewController.UpdateAvatar(previewModel);
        }

        public void Dispose()
        {
            eventBus.EquipEvent -= OnEquipped;
            eventBus.UnEquipEvent -= OnUnequipped;
        }
    }
}
