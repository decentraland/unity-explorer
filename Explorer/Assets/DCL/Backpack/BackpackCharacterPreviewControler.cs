using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables.Components;
using DCL.Backpack.BackpackBus;
using DCL.Optimization.Pools;
using DCL.Profiles;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Avatar = DCL.Profiles.Avatar;

namespace DCL.CharacterPreview
{
    public class BackpackCharacterPreviewControler : IDisposable
    {
        private readonly BackpackCharacterPreviewView view;
        private readonly ICharacterPreviewFactory previewFactory;
        private readonly BackpackEventBus backpackEventBus;
        private readonly CharacterPreviewInputEventBus inputEventBus;

        private IComponentPoolsRegistry poolsRegistry;

        private World world;
        private Entity playerEntity;
        private CharacterPreviewController previewController;
        private CharacterPreviewModel previewModel;

        public BackpackCharacterPreviewControler(BackpackCharacterPreviewView view, ICharacterPreviewFactory previewFactory, BackpackEventBus backpackEventBus, IComponentPoolsRegistry poolsRegistry, CharacterPreviewInputEventBus inputEventBus)
        {
            this.view = view;
            this.previewFactory = previewFactory;
            this.backpackEventBus = backpackEventBus;
            this.backpackEventBus.EquipEvent += OnEquipped;
            this.backpackEventBus.UnEquipEvent += OnUnequipped;
            this.poolsRegistry = poolsRegistry;
            this.inputEventBus = inputEventBus;
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
            previewController = previewFactory.Create(world, poolsRegistry, (RenderTexture)view.RawImage.texture, inputEventBus);

            view.CharacterPreviewInputDetector.OnScrollEvent += OnScroll;
            view.CharacterPreviewInputDetector.OnDraggingEvent += OnDrag;
        }

        private void OnScroll(PointerEventData pointerEventData)
        {
            inputEventBus.OnScroll(pointerEventData);
        }

        private void OnDrag(PointerEventData pointerEventData)
        {
            inputEventBus.OnDrag(pointerEventData);
        }

        public void OnHide()
        {

        }

        private void OnEquipped(IWearable i)
        {
            previewModel.Wearables ??= new List<string>();
            previewModel.Wearables.Add(i.GetUrn());
            UpdateModel();
        }

        private void OnUnequipped(IWearable i)
        {
            previewModel.Wearables.Remove(i.GetUrn());
            UpdateModel();
        }

        private void UpdateModel()
        {
            previewController.UpdateAvatar(previewModel);
        }

        public void Dispose()
        {
            backpackEventBus.EquipEvent -= OnEquipped;
            backpackEventBus.UnEquipEvent -= OnUnequipped;
            view.CharacterPreviewInputDetector.OnScrollEvent -= OnScroll;
            view.CharacterPreviewInputDetector.OnDraggingEvent -= OnDrag;
            previewController.Dispose();
        }
    }
}
