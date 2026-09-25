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
        private const float MAX_RENDER_TARGET_PIXELS = 1920f * 1080f;
        private const int MAX_MSAA_4X_RENDER_TARGET_PIXELS = 1024 * 1024;

        private static readonly Vector2Int PLACEHOLDER_RENDER_TARGET_SIZE = new (64, 64);

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
        private bool renderTargetSizeDirty;
        private Vector2Int lastScreenSize;
        public RenderTexture CurrentRenderTexture => currentRenderTexture;

        /// <summary>
        ///     Raised when the render texture is created, with the preview camera already targeting it. A later resize keeps the
        ///     same texture and camera, so it does not raise this.
        /// </summary>
        public event Action? RenderTargetChanged;

        protected Camera? PreviewCamera => previewController?.Camera;

        /// <summary>
        ///     The image stays invisible while a look loads and fades in once it is on. A preview whose render target has
        ///     content of its own before the avatar arrives keeps the image visible from the moment the target exists instead.
        /// </summary>
        protected virtual bool hideImageWhileLoading => true;

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

            if (!hideImageWhileLoading)
                ShowRawImage();

            previewController = previewFactory.Create(world, view.RawImage.rectTransform, currentRenderTexture,
                inputEventBus, view.CharacterPreviewSettingsSo.cameraSettings, avatarPosition);
            initialized = true;

            lastScreenSize = new Vector2Int(Screen.width, Screen.height);
            renderTargetSizeDirty = false;
            Canvas.willRenderCanvases += FitRenderTargetToView;

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
            Canvas.willRenderCanvases -= FitRenderTargetToView;
            characterPreviewEventBus.OnAnyCharacterPreviewShowEvent -= OnAnyCharacterPreviewShow;
            characterPreviewEventBus.OnAnyCharacterPreviewHideEvent -= OnAnyCharacterPreviewHide;
            cursorController.Dispose();
            updateModelCancellationToken.SafeCancelAndDispose();
        }

        // Sized from the pixels the RawImage covers on screen, so a stretched rect (zero sizeDelta) gets a texture matching the view.
        // Capped to MAX_RENDER_TARGET_PIXELS, keeping the aspect ratio
        private Vector2Int RenderTargetSize()
        {
            RectTransform rectTransform = view.RawImage.rectTransform;
            Canvas? canvas = view.RawImage.canvas;

            // Not laid out yet: a tiny target that OnViewRectDimensionsChanged replaces once the rect resolves
            if (canvas == null) return PLACEHOLDER_RENDER_TARGET_SIZE;

            Camera? canvasCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

            Vector2 min = RectTransformUtility.WorldToScreenPoint(canvasCamera, rectTransform.TransformPoint(rectTransform.rect.min));
            Vector2 max = RectTransformUtility.WorldToScreenPoint(canvasCamera, rectTransform.TransformPoint(rectTransform.rect.max));

            float width = Mathf.Abs(max.x - min.x);
            float height = Mathf.Abs(max.y - min.y);

            if (width < 1f || height < 1f) return PLACEHOLDER_RENDER_TARGET_SIZE;

            float pixels = width * height;

            if (pixels > MAX_RENDER_TARGET_PIXELS)
            {
                float scale = Mathf.Sqrt(MAX_RENDER_TARGET_PIXELS / pixels);
                width *= scale;
                height *= scale;
            }

            return new Vector2Int(Mathf.Max(1, Mathf.RoundToInt(width)), Mathf.Max(1, Mathf.RoundToInt(height)));
        }

        private static RenderTexture CreateRenderTexture(Vector2Int size)
        {
            //Temporal solution to fix issue with render format in Mac VS Windows
            var renderTexture = new RenderTexture(size.x, size.y, 16, TextureUtilities.GetColorSpaceFormat())
            {
                name = "Preview Texture",
                antiAliasing = AntiAliasingFor(size),
                useDynamicScale = true,
            };

            renderTexture.Create();
            return renderTexture;
        }

        // Every MSAA sample multiplies the colour and depth memory, only small panel-sized targets can afford 4x
        private static int AntiAliasingFor(Vector2Int size) =>
            size.x * size.y > MAX_MSAA_4X_RENDER_TARGET_PIXELS ? 2 : 4;

        private void OnViewRectDimensionsChanged()
        {
            renderTargetSizeDirty = true;
        }

        // Runs once per frame after the canvas scaler has applied this frame's scale, so the rect maps to settled screen pixels.
        // The rect callback alone is not enough: it fires while the scaler is still mid-update, and a scaled canvas keeps the
        // same rect across resolutions with the same aspect ratio, so the screen size is compared as well
        private void FitRenderTargetToView()
        {
            if (!initialized || currentRenderTexture == null) return;

            var screenSize = new Vector2Int(Screen.width, Screen.height);

            if (screenSize != lastScreenSize)
            {
                lastScreenSize = screenSize;
                renderTargetSizeDirty = true;
            }

            if (!renderTargetSizeDirty) return;

            renderTargetSizeDirty = false;

            Vector2Int size = RenderTargetSize();
            if (size.x == currentRenderTexture.width && size.y == currentRenderTexture.height) return;

            // Resized in place, so the raw image and the preview camera keep pointing at the same texture object
            currentRenderTexture.Release();
            currentRenderTexture.width = size.x;
            currentRenderTexture.height = size.y;
            currentRenderTexture.antiAliasing = AntiAliasingFor(size);
            currentRenderTexture.Create();

            // The camera only reads the aspect off its target when the target is assigned, so a resize leaves it rendering with the old one
            PreviewCamera?.ResetAspect();
        }

        private void ReleaseRenderTexture()
        {
            Canvas.willRenderCanvases -= FitRenderTargetToView;

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

            if (!hideImageWhileLoading) return;

            profileColor.a = 1;
            view.RawImage.DOColor(profileColor, AVATAR_FADE_ANIMATION);
        }

        private GameObject EnableSpinner()
        {
            if (hideImageWhileLoading)
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

        private void ShowRawImage()
        {
            view.RawImage.DOKill();
            profileColor.a = 1;
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
