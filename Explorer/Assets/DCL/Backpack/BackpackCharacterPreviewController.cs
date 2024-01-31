using Arch.Core;
using Arch.SystemGroups;
using DCL.AvatarRendering.Wearables.Components;
using DCL.Backpack.BackpackBus;
using DCL.Profiles;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Experimental.Rendering;
using Avatar = DCL.Profiles.Avatar;

namespace DCL.CharacterPreview
{
    public class BackpackCharacterPreviewController : IDisposable
    {

        private readonly BackpackCharacterPreviewView view;
        private readonly ICharacterPreviewFactory previewFactory;
        private readonly BackpackEventBus backpackEventBus;
        private readonly CharacterPreviewInputEventBus inputEventBus;

        private World world;
        private CharacterPreviewController previewController;
        private CharacterPreviewModel previewModel;
        private readonly BackpackCharacterPreviewCursorController cursorController;

        public BackpackCharacterPreviewController(BackpackCharacterPreviewView view, ICharacterPreviewFactory previewFactory, BackpackEventBus backpackEventBus, CharacterPreviewInputEventBus inputEventBus)
        {
            this.view = view;
            this.previewFactory = previewFactory;
            this.backpackEventBus = backpackEventBus;
            this.inputEventBus = inputEventBus;

            backpackEventBus.EquipEvent += OnEquipped;
            backpackEventBus.UnEquipEvent += OnUnequipped;
            backpackEventBus.FilterCategoryByEnumEvent += OnChangeCategory;
            backpackEventBus.ForceRenderEvent += OnForceRenderChange;

            view.CharacterPreviewInputDetector.OnScrollEvent += OnScroll;
            view.CharacterPreviewInputDetector.OnDraggingEvent += OnDrag;
            view.CharacterPreviewInputDetector.OnPointerUpEvent += OnPointerUp;
            view.CharacterPreviewInputDetector.OnPointerDownEvent += OnPointerDown;

            cursorController = new BackpackCharacterPreviewCursorController(view.CharacterPreviewCursorView, inputEventBus);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in Entity playerEntity)
        {
            world = builder.World;
            InitializePreviewModel(playerEntity);
        }

        private void InitializePreviewModel(Entity playerEntity)
        {
            Avatar avatar = world.Get<Profile>(playerEntity).Avatar;

            previewModel.BodyShape = avatar.BodyShape;
            previewModel.HairColor = avatar.HairColor;
            previewModel.SkinColor = avatar.SkinColor;
            previewModel.ForceRender = new HashSet<string>(avatar.ForceRender);
        }

        public void Initialize()
        {
            //Temporal solution to fix issue with render format in Mac VS Windows
            if (Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer)
            {
                var sizeDelta = view.RawImage.rectTransform.sizeDelta;

                var newTexture = new RenderTexture((int)sizeDelta.x,(int)sizeDelta.y, 0, GraphicsFormat.A2R10G10B10_UNormPack32)
                {
                    name = "Preview Texture",
                };
                newTexture.Create();

                view.RawImage.texture = newTexture;
                previewController = previewFactory.Create(world, newTexture, inputEventBus);
            }
            else
            {
                var sizeDelta = view.RawImage.rectTransform.sizeDelta;

                var newTexture = new RenderTexture((int)sizeDelta.x,(int)sizeDelta.y, 0, GraphicsFormat.R32G32B32A32_SInt)
                {
                    name = "Preview Texture",
                };
                newTexture.Create();
                view.RawImage.texture = newTexture;
                previewController = previewFactory.Create(world, newTexture, inputEventBus);
            }

            OnModelUpdated();
        }

        private void OnPointerUp(PointerEventData pointerEventData)
        {
            inputEventBus.OnPointerUp(pointerEventData);
        }

        private void OnPointerDown(PointerEventData pointerEventData)
        {
            inputEventBus.OnPointerDown(pointerEventData);
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

        private void OnForceRenderChange(IReadOnlyCollection<string> forceRender)
        {
            previewModel.ForceRender.Clear();
            
            foreach (string wearable in forceRender)
            {
                previewModel.ForceRender.Add(wearable);
            }
            
            OnModelUpdated();
        }

        public void OnShow()
        {
            previewController.Show();
            OnModelUpdated();
        }

        public void OnHide()
        {
            previewController.Hide();
        }

        private void OnEquipped(IWearable i)
        {
            previewModel.Wearables ??= new List<string>();

            if (i.IsBodyShape())
                previewModel.BodyShape = i.GetUrn();
            else previewModel.Wearables.Add(i.GetUrn());

            OnModelUpdated();
        }

        private void OnUnequipped(IWearable i)
        {
            previewModel.Wearables.Remove(i.GetUrn());
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
            view.CharacterPreviewInputDetector.OnPointerUpEvent -= OnPointerUp;
            view.CharacterPreviewInputDetector.OnPointerDownEvent -= OnPointerDown;
            previewController.Dispose();
            cursorController.Dispose();
        }
    }
}
