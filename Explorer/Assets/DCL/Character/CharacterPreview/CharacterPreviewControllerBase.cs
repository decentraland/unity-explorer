using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Audio;
using DG.Tweening;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using Utility;
using Avatar = DCL.Profiles.Avatar;

namespace DCL.CharacterPreview
{
    public abstract class CharacterPreviewControllerBase : IDisposable
    {
        private static readonly float AVATAR_FADE_ANIMATION = 0.5f;
        protected readonly CharacterPreviewInputEventBus inputEventBus;

        private readonly CharacterPreviewView view;
        private readonly ICharacterPreviewFactory previewFactory;
        private readonly CharacterPreviewCursorController cursorController;
        private readonly CharacterPreviewEventBus characterPreviewEventBus;

        private readonly World world;
        protected CharacterPreviewAvatarModel previewAvatarModel;

        protected bool zoomEnabled = true;
        protected bool panEnabled = true;
        protected bool rotateEnabled = true;
        private CharacterPreviewController? previewController;
        private bool initialized;
        private CancellationTokenSource updateModelCancellationToken;
        private Color profileColor;
        private bool isPreviewPlatformActive;
        private CharacterPreviewType characterPreviewType;

        protected CharacterPreviewControllerBase(
            CharacterPreviewView view,
            ICharacterPreviewFactory previewFactory,
            World world,
            bool isPreviewPlatformActive,
            CharacterPreviewType characterPreviewType,
            CharacterPreviewEventBus characterPreviewEventBus)
        {
            this.view = view;
            this.previewFactory = previewFactory;
            this.world = world;
            this.isPreviewPlatformActive = isPreviewPlatformActive;
            this.characterPreviewType = characterPreviewType;
            this.characterPreviewEventBus = characterPreviewEventBus;

            if (view.EnableZooming)
                view.CharacterPreviewInputDetector.OnScrollEvent += OnScroll;

            view.CharacterPreviewInputDetector.OnPointerEnterEvent += OnPointerEnter;
            view.CharacterPreviewInputDetector.OnDraggingEvent += OnDrag;
            view.CharacterPreviewInputDetector.OnPointerUpEvent += OnPointerUp;
            view.CharacterPreviewInputDetector.OnPointerDownEvent += OnPointerDown;

            inputEventBus = new CharacterPreviewInputEventBus();
            cursorController = new CharacterPreviewCursorController(view.CharacterPreviewCursorContainer, inputEventBus, view.CharacterPreviewSettingsSo.cursorSettings);

            characterPreviewEventBus.OnAnyCharacterPreviewShowEvent += OnAnyCharacterPreviewShow;
            characterPreviewEventBus.OnAnyCharacterPreviewHideEvent += OnAnyCharacterPreviewHide;
        }

        public virtual void Initialize(Avatar avatar)
        {
            previewAvatarModel.BodyShape = avatar.BodyShape;
            previewAvatarModel.HairColor = avatar.HairColor;
            previewAvatarModel.SkinColor = avatar.SkinColor;
            previewAvatarModel.EyesColor = avatar.EyesColor;
            previewAvatarModel.ForceRenderCategories = new HashSet<string>(avatar.ForceRender);
            previewAvatarModel.Initialized = true;

            Initialize();
        }

        private void Initialize()
        {
            if (initialized) return;

            //Temporal solution to fix issue with render format in Mac VS Windows
            Vector2 sizeDelta = view.RawImage.rectTransform.sizeDelta;

            var newTexture = new RenderTexture((int)sizeDelta.x, (int)sizeDelta.y, 0, TextureUtilities.GetColorSpaceFormat())
            {
                name = "Preview Texture",
            };

            newTexture.antiAliasing = 8;
            newTexture.useDynamicScale = true;
            newTexture.Create();

            view.RawImage.texture = newTexture;

            previewController = previewFactory.Create(world, newTexture, inputEventBus, view.CharacterPreviewSettingsSo.cameraSettings);
            initialized = true;

            OnModelUpdated();
        }

        public void Dispose()
        {
            initialized = false;
            previewController?.Dispose();
            view.CharacterPreviewInputDetector.OnScrollEvent -= OnScroll;
            view.CharacterPreviewInputDetector.OnDraggingEvent -= OnDrag;
            view.CharacterPreviewInputDetector.OnPointerUpEvent -= OnPointerUp;
            view.CharacterPreviewInputDetector.OnPointerDownEvent -= OnPointerDown;
            view.CharacterPreviewInputDetector.OnPointerEnterEvent -= OnPointerEnter;
            characterPreviewEventBus.OnAnyCharacterPreviewShowEvent -= OnAnyCharacterPreviewShow;
            characterPreviewEventBus.OnAnyCharacterPreviewHideEvent -= OnAnyCharacterPreviewHide;
            cursorController.Dispose();
            updateModelCancellationToken.SafeCancelAndDispose();
        }

        private void OnPointerEnter(PointerEventData pointerEventData)
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(view.HoverAudio);
        }

        private void OnPointerUp(PointerEventData pointerEventData)
        {
            if ((pointerEventData.button == PointerEventData.InputButton.Right && view.EnablePanning && panEnabled) ||
                (pointerEventData.button == PointerEventData.InputButton.Left && view.EnableRotating && rotateEnabled))
                inputEventBus.OnPointerUp(pointerEventData);
        }

        private void OnPointerDown(PointerEventData pointerEventData)
        {
            if ((pointerEventData.button == PointerEventData.InputButton.Right && view.EnablePanning && panEnabled) ||
                (pointerEventData.button == PointerEventData.InputButton.Left && view.EnableRotating && rotateEnabled))
                inputEventBus.OnPointerDown(pointerEventData);
        }

        private void OnScroll(PointerEventData pointerEventData)
        {
            if (zoomEnabled)
            {
                inputEventBus.OnScroll(pointerEventData);

                UIAudioEventsBus.Instance.SendPlayAudioEvent(pointerEventData.scrollDelta.y > 0 ? view.ZoomInAudio : view.ZoomOutAudio);
            }
        }

        private void OnDrag(PointerEventData pointerEventData)
        {
            if ((pointerEventData.button == PointerEventData.InputButton.Right && view.EnablePanning && panEnabled) ||
                (pointerEventData.button == PointerEventData.InputButton.Left && view.EnableRotating && rotateEnabled))
            {
                inputEventBus.OnDrag(pointerEventData);

                switch (pointerEventData.button)
                {
                    case PointerEventData.InputButton.Right when view.EnablePanning:
                        UIAudioEventsBus.Instance.SendPlayAudioEvent(view.VerticalPanAudio);
                        break;
                    case PointerEventData.InputButton.Left when view.EnableRotating:
                        UIAudioEventsBus.Instance.SendPlayAudioEvent(view.RotateAudio);
                        break;
                }
            }
        }

        public void OnShow()
        {
            if (initialized)
            {
                inputEventBus.OnChangePreviewFocus(AvatarWearableCategoryEnum.Body);
                OnModelUpdated();
            }
            else if (previewAvatarModel.Initialized) { Initialize(); }

            previewController?.SetPreviewPlatformActive(isPreviewPlatformActive);
            characterPreviewEventBus.OnAnyCharacterPreviewShow(characterPreviewType);
        }

        public void OnHide()
        {
            if (initialized)
            {
                updateModelCancellationToken.SafeCancelAndDispose();
                previewController?.Dispose();
                previewController = null;
                initialized = false;
            }

            characterPreviewEventBus.OnAnyCharacterPreviewHide(characterPreviewType);
        }

        // If another character preview is shown, we deactivate the current one in order to avoid rendering issues.
        // We can only have one character preview active at a time.
        private void OnAnyCharacterPreviewShow(CharacterPreviewType type)
        {
            if (type == characterPreviewType)
                return;

            previewController?.SetCharacterPreviewAvatarContainerActive(false);
        }

        // Once any other character preview is closed, we activate back the current one.
        private void OnAnyCharacterPreviewHide(CharacterPreviewType type)
        {
            if (type == characterPreviewType)
                return;

            previewController?.SetCharacterPreviewAvatarContainerActive(true);
        }

        protected void OnModelUpdated()
        {
            updateModelCancellationToken = updateModelCancellationToken.SafeRestart();
            ShowLoadingSpinnerAndUpdateAvatarAsync(updateModelCancellationToken.Token).Forget();
        }

        protected async UniTask ShowLoadingSpinnerAndUpdateAvatarAsync(CancellationToken ct)
        {
            GameObject spinner = EnableSpinner();

            await UpdateAvatarAsync(previewAvatarModel, ct);

            DisableSpinner(spinner);
        }


        private void DisableSpinner(GameObject spinner)
        {
            spinner.SetActive(false);
            profileColor.a = 1;
            view.RawImage.DOColor(profileColor, AVATAR_FADE_ANIMATION);
        }

        private GameObject EnableSpinner()
        {
            profileColor = view.RawImage.color;
            profileColor.a = 0;
            view.RawImage.color = profileColor;
            GameObject spinner = view.Spinner;
            spinner.SetActive(true);
            return spinner;
        }

        private async UniTask UpdateAvatarAsync(CharacterPreviewAvatarModel model, CancellationToken ct)
        {
            try { await (previewController?.UpdateAvatarAsync(model, ct) ?? UniTask.CompletedTask); }
            catch (OperationCanceledException) { }
        }

        protected void StopEmotes() =>
            previewController?.StopEmotes();

        protected void PlayEmote(string emoteId) =>
            previewController?.PlayEmote(emoteId);
    }
}
