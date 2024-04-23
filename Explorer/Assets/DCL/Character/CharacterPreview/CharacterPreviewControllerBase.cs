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

        private readonly World world;
        protected CharacterPreviewAvatarModel previewAvatarModel;
        private CharacterPreviewController? previewController;
        private bool initialized;
        private CancellationTokenSource cancellationTokenSource;

        protected CharacterPreviewControllerBase(CharacterPreviewView view, ICharacterPreviewFactory previewFactory, World world)
        {
            this.view = view;
            this.previewFactory = previewFactory;
            this.world = world;
            if(view.EnableZooming)
                view.CharacterPreviewInputDetector.OnScrollEvent += OnScroll;

            view.CharacterPreviewInputDetector.OnPointerMoveEvent += OnPointerMove;
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
            previewAvatarModel.EyesColor = avatar.EyesColor;
            previewAvatarModel.ForceRenderCategories = new HashSet<string>(avatar.ForceRender);
            previewAvatarModel.Initialized = true;

            Initialize();
        }

        private void Initialize()
        {
            //Temporal solution to fix issue with render format in Mac VS Windows
            Vector2 sizeDelta = view.RawImage.rectTransform.sizeDelta;

            var newTexture = new RenderTexture((int)sizeDelta.x, (int)sizeDelta.y, 0, TextureUtilities.GetColorSpaceFormat())
            {
                name = "Preview Texture",
            };

            newTexture.Create();

            view.RawImage.texture = newTexture;

            previewController = previewFactory.Create(world, newTexture, inputEventBus, view.CharacterPreviewSettingsSo.cameraSettings);
            initialized = true;

            OnModelUpdated();
        }

        public void Dispose()
        {
            previewController?.Dispose();
            view.CharacterPreviewInputDetector.OnScrollEvent -= OnScroll;
            view.CharacterPreviewInputDetector.OnDraggingEvent -= OnDrag;
            view.CharacterPreviewInputDetector.OnPointerUpEvent -= OnPointerUp;
            view.CharacterPreviewInputDetector.OnPointerDownEvent -= OnPointerDown;
            view.CharacterPreviewInputDetector.OnPointerMoveEvent -= OnPointerMove;
            cursorController.Dispose();
        }

        private void OnPointerMove(PointerEventData pointerEventData)
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(view.HoverAudio);
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

            if (pointerEventData.scrollDelta.y > 0)
            {
                UIAudioEventsBus.Instance.SendPlayAudioEvent(view.ZoomInAudio);
            }
            else
            {
                UIAudioEventsBus.Instance.SendPlayAudioEvent(view.ZoomOutAudio);
            }
        }

        private void OnDrag(PointerEventData pointerEventData)
        {
            if((pointerEventData.button == PointerEventData.InputButton.Right && view.EnablePanning) ||
               (pointerEventData.button == PointerEventData.InputButton.Left && view.EnableRotating))
                inputEventBus.OnDrag(pointerEventData);

            if (pointerEventData.button != PointerEventData.InputButton.Middle)
            {
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
        }

        public void OnHide()
        {
            if (initialized)
            {
                previewController?.Dispose();
                previewController = null;
                initialized = false;
            }
        }

        protected void OnModelUpdated()
        {
            cancellationTokenSource.SafeCancelAndDispose();
            cancellationTokenSource = new CancellationTokenSource();
            WrapInSpinnerAsync(cancellationTokenSource.Token).Forget();
        }

        private Color profileColor;

        private async UniTaskVoid WrapInSpinnerAsync(CancellationToken ct)
        {
            profileColor = view.RawImage.color;
            profileColor.a = 0;
            view.RawImage.color = profileColor;
            var spinner = view.Spinner;
            spinner.SetActive(true);
            await (previewController?.UpdateAvatar(previewAvatarModel, ct) ?? UniTask.CompletedTask);
            spinner.SetActive(false);
            profileColor.a = 1;
            view.RawImage.DOColor(profileColor, AVATAR_FADE_ANIMATION);
        }

        protected void PlayEmote(string emoteId) =>
            previewController?.PlayEmote(emoteId);
    }
}
