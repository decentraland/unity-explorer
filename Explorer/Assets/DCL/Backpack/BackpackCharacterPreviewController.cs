using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables.Components;
using DCL.Backpack;
using DCL.Backpack.BackpackBus;
using DCL.Optimization.Pools;
using DCL.Profiles;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Avatar = DCL.Profiles.Avatar;

namespace DCL.CharacterPreview
{
    public class BackpackCharacterPreviewController : IDisposable
    {
        private readonly BackpackCharacterPreviewView view;
        private readonly ICharacterPreviewFactory previewFactory;
        private readonly BackpackEventBus backpackEventBus;
        private readonly CharacterPreviewInputEventBus inputEventBus;
        private readonly IComponentPoolsRegistry poolsRegistry;

        private World world;
        private CharacterPreviewController previewController;
        private CharacterPreviewModel previewModel;
        private Entity myPlayerEntity;

        public BackpackCharacterPreviewController(BackpackCharacterPreviewView view, ICharacterPreviewFactory previewFactory, BackpackEventBus backpackEventBus, IComponentPoolsRegistry poolsRegistry, CharacterPreviewInputEventBus inputEventBus)
        {
            this.view = view;
            this.previewFactory = previewFactory;
            this.backpackEventBus = backpackEventBus;
            this.poolsRegistry = poolsRegistry;
            this.inputEventBus = inputEventBus;

            this.backpackEventBus.EquipEvent += OnEquipped;
            this.backpackEventBus.UnEquipEvent += OnUnequipped;
            this.backpackEventBus.FilterCategoryByEnumEvent += OnChangeCategory;

            view.CharacterPreviewInputDetector.OnScrollEvent += OnScroll;
            view.CharacterPreviewInputDetector.OnDraggingEvent += OnDrag;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in Entity playerEntity)
        {
            world = builder.World;
            InitializePreviewModel(playerEntity);
        }

        private void InitializePreviewModel(Entity playerEntity)
        {
            myPlayerEntity = playerEntity;
            Avatar avatar = world.Get<Profile>(playerEntity).Avatar;

            previewModel.BodyShape = avatar.BodyShape;
            previewModel.HairColor = avatar.HairColor;
            previewModel.SkinColor = avatar.SkinColor;
        }

        public void Initialize()
        {
            previewController = previewFactory.Create(world, poolsRegistry, (RenderTexture)view.RawImage.texture, inputEventBus, myPlayerEntity);
            OnModelUpdated();
        }


        private void OnScroll(PointerEventData pointerEventData)
        {
            inputEventBus.OnScroll(pointerEventData);
        }

        private void OnDrag(PointerEventData pointerEventData)
        {
            inputEventBus.OnDrag(pointerEventData);
        }

        private void OnChangeCategory(AvatarSlotCategoryEnum categoryEnum)
        {
            inputEventBus.OnChangeCategoryFocus(categoryEnum);
        }

        public void OnShow()
        {
            previewController.Show();
        }

        public void OnHide()
        {
            previewController.Hide();
        }

        private void OnEquipped(IWearable i)
        {
            previewModel.Wearables ??= new List<string>();

            if (!previewModel.Wearables.Contains(i.GetUrn()))
            {
                previewModel.Wearables.Add(i.GetUrn());
            }
            OnModelUpdated();
        }

        private void OnUnequipped(IWearable i)
        {
            if (previewModel.Wearables.Contains(i.GetUrn()))
            {
                previewModel.Wearables.Remove(i.GetUrn());
            }
            OnModelUpdated();
        }

        private void OnModelUpdated()
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
