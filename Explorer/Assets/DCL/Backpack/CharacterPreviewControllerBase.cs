
using Arch.Core;
using CommunicationData.URLHelpers;
using DCL.Profiles;
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
        private protected readonly CharacterPreviewInputEventBus inputEventBus;
        private protected CharacterPreviewAvatarModel previewAvatarModel;

        private readonly CharacterPreviewView view;
        private readonly ICharacterPreviewFactory previewFactory;
        private readonly CharacterPreviewCursorController cursorController;

        private protected World world;
        private CharacterPreviewController previewController;

        private protected CharacterPreviewControllerBase(CharacterPreviewView view, ICharacterPreviewFactory previewFactory)
        {
            this.view = view;
            this.previewFactory = previewFactory;
            view.CharacterPreviewInputDetector.OnScrollEvent += OnScroll;
            view.CharacterPreviewInputDetector.OnDraggingEvent += OnDrag;
            view.CharacterPreviewInputDetector.OnPointerUpEvent += OnPointerUp;
            view.CharacterPreviewInputDetector.OnPointerDownEvent += OnPointerDown;

            inputEventBus = new CharacterPreviewInputEventBus();
            cursorController = new CharacterPreviewCursorController(view.CharacterPreviewCursorContainer, inputEventBus, view.CharacterPreviewSettingsSo.cursorSettings);
        }

        public virtual void Initialize(Avatar avatar)
        {
            previewAvatarModel.BodyShape = avatar.BodyShape;
            previewAvatarModel.HairColor = avatar.HairColor;
            previewAvatarModel.SkinColor = avatar.SkinColor;
            previewAvatarModel.ForceRenderCategories = new HashSet<string>(avatar.ForceRender);

            //Temporal solution to fix issue with render format in Mac VS Windows
            Vector2 sizeDelta = view.RawImage.rectTransform.sizeDelta;
            var newTexture = new RenderTexture((int)sizeDelta.x, (int)sizeDelta.y, 0, TextureUtilities.GetColorSpaceFormat())
            {
                name = "Preview Texture",
            };

            newTexture.Create();

            view.RawImage.texture = newTexture;

            previewController = previewFactory.Create(world, newTexture, inputEventBus, view.CharacterPreviewSettingsSo.cameraSettings);

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

        private protected void OnModelUpdated()
        {
            previewController.UpdateAvatar(previewAvatarModel);
        }

        public void SetWorld(World world)
        {
            this.world = world;
        }


        public void Dispose()
        {
            previewController.Dispose();
            view.CharacterPreviewInputDetector.OnScrollEvent -= OnScroll;
            view.CharacterPreviewInputDetector.OnDraggingEvent -= OnDrag;
            view.CharacterPreviewInputDetector.OnPointerUpEvent -= OnPointerUp;
            view.CharacterPreviewInputDetector.OnPointerDownEvent -= OnPointerDown;
            cursorController.Dispose();
        }
    }
}
