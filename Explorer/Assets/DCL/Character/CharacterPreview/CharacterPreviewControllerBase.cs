using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.AvatarRendering.Emotes;
using DCL.Diagnostics;
using DG.Tweening;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using Utility;
using Avatar = DCL.Profiles.Avatar;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace DCL.CharacterPreview
{
    public abstract class CharacterPreviewControllerBase : IDisposable
    {
        private const float AVATAR_FADE_ANIMATION = 0.5f;

        private readonly List<string> randomBasicEmotes = new()
        {
            "wave", "fistpump", "dab"
        };

        protected readonly CharacterPreviewInputEventBus inputEventBus;

        protected readonly CharacterPreviewView view;
        private readonly ICharacterPreviewFactory previewFactory;
        private readonly CharacterPreviewCursorController cursorController;
        private readonly CharacterPreviewEventBus characterPreviewEventBus;
        private readonly World world;
        private readonly bool isPreviewPlatformActive;
        private readonly Func<bool> isPlayingEmoteDelegate;

        private bool initialized;
        private CancellationTokenSource? updateModelCancellationToken;
        private Color profileColor;
        private Vector3 avatarPosition;

        private RenderTexture? currentRenderTexture;
        public RenderTexture CurrentRenderTexture => currentRenderTexture;

        /// <summary>
        ///     Raised when the render texture is created or recreated after a resize, with the preview camera already targeting it.
        /// </summary>
        public event Action? RenderTargetChanged;

        protected Camera? PreviewCamera => previewController?.Camera;

        protected CharacterPreviewController? previewController;
        protected CharacterPreviewAvatarModel previewAvatarModel;
        protected bool zoomEnabled = true;
        protected bool panEnabled = true;
        protected bool rotateEnabled = true;

        protected CharacterPreviewControllerBase(
            CharacterPreviewView view,
            ICharacterPreviewFactory previewFactory,
            World world,
            bool isPreviewPlatformActive,
            CharacterPreviewEventBus characterPreviewEventBus)
        {
            this.view = view;
            this.previewFactory = previewFactory;
            this.world = world;
            this.isPreviewPlatformActive = isPreviewPlatformActive;
            this.characterPreviewEventBus = characterPreviewEventBus;

            if (view.EnableZooming)
                view.CharacterPreviewInputDetector.OnScrollEvent += OnScroll;

            view.CharacterPreviewInputDetector.OnPointerEnterEvent += OnPointerEnter;
            view.CharacterPreviewInputDetector.OnDraggingEvent += OnDrag;
            view.CharacterPreviewInputDetector.OnPointerUpEvent += OnPointerUp;
            view.CharacterPreviewInputDetector.OnPointerDownEvent += OnPointerDown;
            view.RectDimensionsChanged += OnViewRectDimensionsChanged;

            inputEventBus = new CharacterPreviewInputEventBus();
            cursorController = new CharacterPreviewCursorController(view.CharacterPreviewCursorContainer, inputEventBus, view.CharacterPreviewSettingsSo.cursorSettings);

            characterPreviewEventBus.OnAnyCharacterPreviewShowEvent += OnAnyCharacterPreviewShow;
            characterPreviewEventBus.OnAnyCharacterPreviewHideEvent += OnAnyCharacterPreviewHide;

            isPlayingEmoteDelegate = () => previewController?.IsPlayingEmote() ?? false;

            ClearRawImage();
        }

        public virtual void Initialize(Avatar avatar, Vector3 position)
        {
            previewAvatarModel.BodyShape = avatar.BodyShape;
            previewAvatarModel.HairColor = avatar.HairColor;
            previewAvatarModel.SkinColor = avatar.SkinColor;
            previewAvatarModel.EyesColor = avatar.EyesColor;
            previewAvatarModel.ForceRenderCategories = new HashSet<string>(avatar.ForceRender);
            previewAvatarModel.Initialized = true;

            avatarPosition = position;

            Initialize();
        }

        private void Initialize()
        {
            if (initialized) return;

            currentRenderTexture = CreateRenderTexture(RenderTargetSize());

            view.RawImage.texture = currentRenderTexture;

            previewController = previewFactory.Create(world, view.RawImage.rectTransform, currentRenderTexture,
                inputEventBus, view.CharacterPreviewSettingsSo.cameraSettings, avatarPosition);
            initialized = true;

            ResetAvatarMovement();
            OnModelUpdated();
            RenderTargetChanged?.Invoke();
        }

        public virtual void Dispose()
        {
            initialized = false;
            previewController?.Dispose();
            view.CharacterPreviewInputDetector.OnScrollEvent -= OnScroll;
            view.CharacterPreviewInputDetector.OnDraggingEvent -= OnDrag;
            view.CharacterPreviewInputDetector.OnPointerUpEvent -= OnPointerUp;
            view.CharacterPreviewInputDetector.OnPointerDownEvent -= OnPointerDown;
            view.CharacterPreviewInputDetector.OnPointerEnterEvent -= OnPointerEnter;
            view.RectDimensionsChanged -= OnViewRectDimensionsChanged;
            characterPreviewEventBus.OnAnyCharacterPreviewShowEvent -= OnAnyCharacterPreviewShow;
            characterPreviewEventBus.OnAnyCharacterPreviewHideEvent -= OnAnyCharacterPreviewHide;
            cursorController.Dispose();
            updateModelCancellationToken.SafeCancelAndDispose();
        }

        // Sized from the pixels the RawImage covers on screen, so a stretched rect (zero sizeDelta) gets a texture matching the view
        private Vector2Int RenderTargetSize()
        {
            RectTransform rectTransform = view.RawImage.rectTransform;
            Canvas canvas = view.RawImage.canvas;
            Camera? canvasCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

            Vector2 min = RectTransformUtility.WorldToScreenPoint(canvasCamera, rectTransform.TransformPoint(rectTransform.rect.min));
            Vector2 max = RectTransformUtility.WorldToScreenPoint(canvasCamera, rectTransform.TransformPoint(rectTransform.rect.max));

            int width = Mathf.RoundToInt(Mathf.Abs(max.x - min.x));
            int height = Mathf.RoundToInt(Mathf.Abs(max.y - min.y));

            return width > 0 && height > 0 ? new Vector2Int(width, height) : new Vector2Int(Screen.width, Screen.height);
        }

        private static RenderTexture CreateRenderTexture(Vector2Int size)
        {
            //Temporal solution to fix issue with render format in Mac VS Windows
            var renderTexture = new RenderTexture(size.x, size.y, 16, TextureUtilities.GetColorSpaceFormat())
            {
                name = "Preview Texture",
                antiAliasing = 4,
                useDynamicScale = true,
            };

            renderTexture.Create();
            return renderTexture;
        }

        private void OnViewRectDimensionsChanged()
        {
            if (!initialized || currentRenderTexture == null) return;

            Vector2Int size = RenderTargetSize();
            if (size.x == currentRenderTexture.width && size.y == currentRenderTexture.height) return;

            ReleaseRenderTexture();
            currentRenderTexture = CreateRenderTexture(size);
            view.RawImage.texture = currentRenderTexture;
            previewController?.SetTargetTexture(currentRenderTexture);
            RenderTargetChanged?.Invoke();
        }

        private void ReleaseRenderTexture()
        {
            if (!currentRenderTexture) return;
            currentRenderTexture.Release();
            Object.Destroy(currentRenderTexture);
            currentRenderTexture = null;
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
                    case PointerEventData.InputButton.Middle:
                    default:
                        ReportHub.LogError(ReportCategory.UI, nameof(InvalidOperationException));
                        break;
                }
            }
        }

        public void OnBeforeShow()
        {
            EnableSpinner();
        }

        /// <summary>
        /// Shows the character preview.
        /// </summary>
        /// <param name="triggerOnShowBusEvent">True for keeping the rest of preview controllers informed about this showing.</param>
        public void OnShow(bool triggerOnShowBusEvent = true)
        {
            if (initialized)
            {
                inputEventBus.OnChangePreviewFocus(AvatarWearableCategoryEnum.Body);
                OnModelUpdated();
            }
            else if (previewAvatarModel.Initialized)
                Initialize();

            previewController?.SetPreviewPlatformActive(isPreviewPlatformActive);

            if (triggerOnShowBusEvent)
                characterPreviewEventBus.OnAnyCharacterPreviewShow(this);
        }

        /// <summary>
        /// Hides the character preview.
        /// </summary>
        /// <param name="triggerOnHideBusEvent">True for keeping the rest of preview controllers informed about this hiding.</param>
        public virtual void OnHide(bool triggerOnHideBusEvent = true)
        {
            if (initialized)
            {
                updateModelCancellationToken.SafeCancelAndDispose();
                previewController?.Dispose();
                previewController = null;
                initialized = false;
                ReleaseRenderTexture();
                ClearRawImage();
            }

            if (triggerOnHideBusEvent)
                characterPreviewEventBus.OnAnyCharacterPreviewHide(this);
        }

        // If another character preview is shown, we deactivate the current one in order to avoid rendering issues.
        // We can only have one character preview active at a time.
        private void OnAnyCharacterPreviewShow(CharacterPreviewControllerBase characterPreviewController)
        {
            if (characterPreviewController == this)
                return;

            previewController?.SetCharacterPreviewAvatarContainerActive(false);
        }

        // Once any other character preview is closed, we activate back the current one.
        private void OnAnyCharacterPreviewHide(CharacterPreviewControllerBase characterPreviewController)
        {
            if (characterPreviewController == this)
                return;

            previewController?.SetCharacterPreviewAvatarContainerActive(true);
        }

        protected void OnModelUpdated()
        {
            updateModelCancellationToken = updateModelCancellationToken.SafeRestart();
            UpdateModelAsync(updateModelCancellationToken.Token).Forget();
            return;

            async UniTaskVoid UpdateModelAsync(CancellationToken ct)
            {
                try
                {
                    await ShowLoadingSpinnerAndUpdateAvatarAsync(ct);
                }
                catch (OperationCanceledException) { }
            }
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
            HideRawImage();
            GameObject spinner = view.Spinner;
            spinner.SetActive(true);
            return spinner;
        }

        /// <summary>
        ///     Leaves the image with nothing to draw at all. With no render target it falls back to whatever texture the prefab
        ///     carries, or to the one that was just destroyed, and either of those renders as a plain white rect.
        /// </summary>
        private void ClearRawImage()
        {
            HideRawImage();
            view.RawImage.texture = null;
        }

        private void HideRawImage()
        {
            view.RawImage.DOKill();
            profileColor = view.RawImage.color;
            profileColor.a = 0;
            view.RawImage.color = profileColor;
        }

        private async UniTask UpdateAvatarAsync(CharacterPreviewAvatarModel model, CancellationToken ct) =>
            await (previewController?.UpdateAvatarAsync(model, ct) ?? UniTask.CompletedTask);

        public void StopEmotes()
        {
            previewController?.StopEmotes();
        }

        public bool IsPlayingEmote() =>
            previewController?.IsPlayingEmote() ?? false;

        protected async UniTask PlayEmoteAndAwaitItAsync(string emoteURN, CancellationToken ct)
        {
            if (previewController == null || !previewController.Value.IsAvatarLoaded()) return;

            PlayEmote(emoteURN);

            await UniTask.WaitUntil(isPlayingEmoteDelegate, cancellationToken: ct);

            if (previewController!.Value.TryGetPlayingEmote(out CharacterEmoteComponent emoteComponent))
                await UniTask.Delay((int)(emoteComponent.PlayingEmoteDuration * 1000), cancellationToken: ct);
        }

        public void PlayRandomEmote()
        {
            previewController?.PlayEmote(randomBasicEmotes[Random.Range(0, randomBasicEmotes.Count)]);
        }

        protected void PlayEmote(string emoteId)
        {
            previewController?.PlayEmote(emoteId);
        }

        public void ResetEmote()
        {
            previewController?.ResetEmote();
        }

        public void ResetAvatarMovement()
        {
            previewController?.ResetAvatarMovement();
        }

        public void ResetZoom()
        {
            previewController?.ResetZoom();
        }

        public void SetPlatformVisible(bool isVisible)
        {
            previewController?.SetPreviewPlatformActive(isVisible);
        }

        protected void SetPostProcessingEnabled(bool enabled)
        {
            previewController?.SetPostProcessingEnabled(enabled);
        }

        protected void SetPreviewLightActive(bool isActive)
        {
            previewController?.SetLightActive(isActive);
        }
    }
}
