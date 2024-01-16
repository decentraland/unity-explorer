using Arch.Core;
using Arch.SystemGroups;
using DCL.AvatarRendering.Wearables.Components;
using DCL.Backpack.BackpackBus;
using DCL.Profiles;
using System.Collections.Generic;

namespace DCL.CharacterPreview
{
    public class BackpackCharacterPreviewControler
    {
        private readonly BackpackCharacterPreviewView view;
        private readonly ICharacterPreviewFactory previewFactory;
        private readonly BackpackEventBus eventBus;

        private World world;
        private Entity playerEntity;
        private CharacterPreviewController previewController;
        private CharacterPreviewModel previewModel;

        public BackpackCharacterPreviewControler(BackpackCharacterPreviewView view, ICharacterPreviewFactory previewFactory, BackpackEventBus eventBus)
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
            InitializePreviewModel();
        }

        private void InitializePreviewModel()
        {
            Avatar avatar = world.Get<Profile>(playerEntity).Avatar;
            previewModel.BodyShape = avatar.BodyShape;
            previewModel.HairColor = avatar.HairColor;
            previewModel.SkinColor = avatar.SkinColor;
        }

        public void Initialize()
        {
            view.Initialize();
            previewController = previewFactory.Create(world, view.CharacterPreviewContainer);
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
            previewController.UpdateAvatar(previewModel);
        }

        public void Dispose()
        {
            eventBus.EquipEvent -= OnEquipped;
            eventBus.UnEquipEvent -= OnUnequipped;
        }
    }
}
