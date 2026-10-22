using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DCL.UI;
using DG.Tweening;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Utility;

namespace DCL.InWorldCamera.CameraReelGallery.Components
{
    /// <summary>
    ///     One thumbnail slot of a gallery row. Slots are pooled with their row, so a slot is bound many times
    ///     and loads each image through the gallery's sprite cache.
    /// </summary>
    public class ReelThumbnailView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [field: Header("References")]
        [field: SerializeField] internal RawImage thumbnailImage { get; private set; } = null!;
        [field: SerializeField] internal LoadingBrightView loadingBrightView { get; private set; } = null!;
        [field: SerializeField] internal RectTransform optionButtonContainer { get; private set; } = null!;
        [field: SerializeField] internal Button button { get; private set; } = null!;
        [field: SerializeField] internal GameObject outline { get; private set; } = null!;

        [field: Header("Configuration")]
        [field: SerializeField] internal Vector3 optionButtonOffset { get; private set; } = new (-11f, -23f, 0);
        [field: SerializeField] internal float scaleFactorOnHover { get; private set; } = 1.03f;
        [field: SerializeField] internal float scaleAnimationDuration { get; private set; } = 0.3f;
        [field: SerializeField] internal float thumbnailLoadedAnimationDuration { get; private set; } = 0.3f;

        private CameraReelOptionButtonController? optionButton;
        private Action<int>? clicked;
        private CancellationTokenSource? loadCts;
        private int flatIndex = -1;
        private bool imageLoaded;
        private bool hovered;

        public CameraReelResponseCompact? Reel { get; private set; }

        /// <summary>
        ///     Runs once per pooled slot; the click and hover wiring is not repeated on every bind.
        /// </summary>
        public void Initialize(CameraReelOptionButtonController? optionButtonController, Action<int> onClicked, Vector2 cellSize)
        {
            optionButton = optionButtonController;
            clicked = onClicked;
            ((RectTransform)transform).sizeDelta = cellSize;
            button.onClick.AddListener(OnClick);

            if (optionButtonController != null)
                optionButtonController.Hide += ToNormal;
        }

        public void Bind(CameraReelResponseCompact reel, int index, ISpriteCache thumbnailCache)
        {
            flatIndex = index;

            // RefreshAllShownItem rebinds the visible slots after every page; restarting would re-request an image mid-load
            if (ReferenceEquals(Reel, reel))
                return;

            Unbind();
            Reel = reel;
            flatIndex = index;
            gameObject.SetActive(true);

            Sprite? cached = thumbnailCache.GetCachedSprite(reel.thumbnailUrl);

            if (cached != null)
            {
                loadingBrightView.FinishLoadingAnimation(thumbnailImage.gameObject);
                Show(cached.texture);
                return;
            }

            thumbnailImage.texture = null;
            loadCts = loadCts.SafeRestart();
            LoadAsync(reel.thumbnailUrl, thumbnailCache, loadCts.Token).Forget();
        }

        public void Unbind()
        {
            loadCts.SafeCancelAndDispose();
            loadCts = null;
            thumbnailImage.DOKill();
            transform.DOKill();
            transform.localScale = Vector3.one;
            outline.SetActive(false);
            thumbnailImage.texture = null;
            imageLoaded = false;
            hovered = false;

            // A recycled row never receives PointerExit, so the shared options button would stay parked here
            if (optionButton != null && optionButtonContainer.childCount > 0 && optionButtonContainer.GetChild(0).gameObject.activeSelf)
                optionButton.HideControl();

            Reel = null;
            flatIndex = -1;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            CameraReelResponseCompact? reel = Reel;

            if (!imageLoaded || reel == null) return;

            hovered = true;
            transform.DOScale(Vector3.one * scaleFactorOnHover, scaleAnimationDuration);
            optionButton?.TryShow(reel, optionButtonContainer.transform, optionButtonOffset);
            outline.SetActive(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!imageLoaded) return;

            if (optionButton == null)
            {
                ToNormal();
                return;
            }

            if (optionButton.IsContextMenuOpen()) return;

            // Hiding the options button raises Hide, which returns this slot to normal
            optionButton.HideControl();
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void OnDestroy()
        {
            button.onClick.RemoveListener(OnClick);

            if (optionButton != null)
                optionButton.Hide -= ToNormal;
        }

        private async UniTaskVoid LoadAsync(string url, ISpriteCache thumbnailCache, CancellationToken ct)
        {
            loadingBrightView.StartLoadingAnimation(thumbnailImage.gameObject);
            Sprite? sprite = null;

            try { sprite = await thumbnailCache.GetSpriteAsync(url, useKtx: true, ct: ct); }
            catch (OperationCanceledException) { return; }
            catch (Exception e) { ReportHub.LogException(e, new ReportData(ReportCategory.CAMERA_REEL)); }

            // The cache resolves null instead of throwing when the token is cancelled
            if (ct.IsCancellationRequested) return;

            loadingBrightView.FinishLoadingAnimation(thumbnailImage.gameObject);

            if (sprite == null) return;

            Show(sprite.texture);
            thumbnailImage.DOFade(1f, thumbnailLoadedAnimationDuration);
        }

        private void Show(Texture texture)
        {
            // The cache keeps textures readable; the gallery never reads pixels back, so the CPU copy is dropped
            if (texture is Texture2D { isReadable: true } readableTexture)
                readableTexture.Apply(false, true);

            // Centre-crop the thumbnail to the cell aspect ratio
            Vector2 cell = ((RectTransform)transform).sizeDelta;
            float originalToSmallerRatio = texture.height * 1f / cell.y;
            float realWidth = originalToSmallerRatio * cell.x;
            float realWidthDiff = texture.width - realWidth;
            thumbnailImage.texture = texture;
            thumbnailImage.uvRect = new Rect(realWidthDiff / 2f / texture.width, 0, (texture.width - realWidthDiff) / texture.width, 1);
            imageLoaded = true;
        }

        private void OnClick()
        {
            if (imageLoaded)
                clicked?.Invoke(flatIndex);
        }

        private void ToNormal()
        {
            if (!hovered) return;

            hovered = false;
            transform.DOScale(Vector3.one, scaleAnimationDuration);
            outline.SetActive(false);
        }
    }
}
