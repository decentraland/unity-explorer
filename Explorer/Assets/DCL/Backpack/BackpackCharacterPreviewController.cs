using Arch.Core;
using DCL.AvatarRendering.Wearables.Components;
using DCL.Backpack.BackpackBus;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Experimental.Rendering;
using Avatar = DCL.Profiles.Avatar;

namespace DCL.CharacterPreview
{
    public abstract class CharacterPreviewControllerBase : IDisposable
    {
        public void Dispose() { }
    }

    public class BackpackCharacterPreviewController : IDisposable
    {
        private readonly CharacterPreviewView view;
        private readonly ICharacterPreviewFactory previewFactory;
        private readonly BackpackEventBus backpackEventBus;
        private readonly CharacterPreviewInputEventBus inputEventBus;
        private readonly CharacterPreviewCursorController cursorController;

        private World world;
        private CharacterPreviewController previewController;
        private CharacterPreviewAvatarModel previewAvatarModel;

        public BackpackCharacterPreviewController(CharacterPreviewView view, ICharacterPreviewFactory previewFactory, BackpackEventBus backpackEventBus)
        {
            this.view = view;
            this.previewFactory = previewFactory;
            this.backpackEventBus = backpackEventBus;
            inputEventBus = new CharacterPreviewInputEventBus();

            backpackEventBus.EquipEvent += OnEquipped;
            backpackEventBus.UnEquipEvent += OnUnequipped;
            backpackEventBus.FilterCategoryByEnumEvent += OnChangeCategory;
            backpackEventBus.ForceRenderEvent += OnForceRenderChange;

            view.CharacterPreviewInputDetector.OnScrollEvent += OnScroll;
            view.CharacterPreviewInputDetector.OnDraggingEvent += OnDrag;
            view.CharacterPreviewInputDetector.OnPointerUpEvent += OnPointerUp;
            view.CharacterPreviewInputDetector.OnPointerDownEvent += OnPointerDown;

            cursorController = new CharacterPreviewCursorController(view.CharacterPreviewCursorContainer, inputEventBus, view.CharacterPreviewSettingsSo.cursorSettings);
        }

        public void Initialize(Avatar avatar)
        {
            previewAvatarModel.BodyShape = avatar.BodyShape;
            previewAvatarModel.HairColor = avatar.HairColor;
            previewAvatarModel.SkinColor = avatar.SkinColor;
            previewAvatarModel.ForceRender = new HashSet<string>(avatar.ForceRender);

            //Temporal solution to fix issue with render format in Mac VS Windows
            if (Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer)
            {
                Vector2 sizeDelta = view.RawImage.rectTransform.sizeDelta;

                var newTexture = new RenderTexture((int)sizeDelta.x, (int)sizeDelta.y, 0, GraphicsFormat.A2R10G10B10_UNormPack32)
                {
                    name = "Preview Texture",
                };

                newTexture.Create();

                view.RawImage.texture = newTexture;
                previewController = previewFactory.Create(world, newTexture, inputEventBus, view.CharacterPreviewSettingsSo.cameraSettings);
            }
            else
            {
                Vector2 sizeDelta = view.RawImage.rectTransform.sizeDelta;

                var newTexture = new RenderTexture((int)sizeDelta.x, (int)sizeDelta.y, 0, GraphicsFormat.R32G32B32A32_SInt)
                {
                    name = "Preview Texture",
                };

                newTexture.Create();
                view.RawImage.texture = newTexture;
                previewController = previewFactory.Create(world, newTexture, inputEventBus, view.CharacterPreviewSettingsSo.cameraSettings);
            }

            OnModelUpdated();
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

        public void SetWorld(World world)
        {
            this.world = world;
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

        public void OnShow()
        {
            previewController.Show();
            inputEventBus.OnChangePreviewFocus(AvatarWearableCategoryEnum.Body);
            OnModelUpdated();
        }

        public void OnHide()
        {
            previewController.Hide();
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

        private void OnModelUpdated()
        {
            previewController.UpdateAvatar(previewAvatarModel);
        }
    }
}
