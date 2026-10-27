using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DCL.UI;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.InWorldCamera.CameraReelGallery.Components
{
    /// <summary>
    ///     Binds reels to one pooled thumbnail slot: slots are bound many times, and each image loads through the
    ///     gallery's sprite cache.
    /// </summary>
    public class ReelThumbnailController : IDisposable
    {
        private readonly ReelThumbnailView view;
        private readonly CameraReelOptionButtonController? optionButton;
        private readonly Action<int> clicked;

        private CancellationTokenSource? loadCts;
        private int flatIndex = -1;
        private bool imageLoaded;
        private bool hovered;

        public CameraReelResponseCompact? Reel { get; private set; }

        public ReelThumbnailController(ReelThumbnailView view, CameraReelOptionButtonController? optionButton, Action<int> clicked)
        {
            this.view = view;
            this.optionButton = optionButton;
            this.clicked = clicked;

            view.PointerEnter += OnPointerEnter;
            view.PointerExit += OnPointerExit;
            view.button.onClick.AddListener(OnClick);

            if (optionButton != null)
                optionButton.Hide += ToNormal;
        }

        public void Dispose()
        {
            Unbind();
            view.PointerEnter -= OnPointerEnter;
            view.PointerExit -= OnPointerExit;
            view.button.onClick.RemoveListener(OnClick);

            if (optionButton != null)
                optionButton.Hide -= ToNormal;
        }

        public void Bind(CameraReelResponseCompact reel, int index, ISpriteCache thumbnailCache)
        {
            // The same reel keeps its in-flight load; only the index may have shifted.
            if (ReferenceEquals(Reel, reel))
            {
                flatIndex = index;
                return;
            }

            Unbind();
            Reel = reel;
            flatIndex = index;
            view.gameObject.SetActive(true);

            Sprite? cached = thumbnailCache.GetCachedSprite(reel.thumbnailUrl);

            if (cached != null)
            {
                view.loadingBrightView.FinishLoadingAnimation(view.thumbnailImage.gameObject);
                Show(cached.texture, fadeIn: false);
                return;
            }

            view.thumbnailImage.texture = null;
            loadCts = loadCts.SafeRestart();
            LoadAsync(reel.thumbnailUrl, thumbnailCache, loadCts.Token).Forget();
        }

        public void Unbind()
        {
            if (Reel == null) return;

            loadCts.SafeCancelAndDispose();
            loadCts = null;
            view.Clear();
            imageLoaded = false;
            hovered = false;

            // A recycled row never receives PointerExit, so the shared options button would stay parked here.
            if (optionButton != null && optionButton.IsShownUnder(view.optionButtonContainer))
                optionButton.HideControl();

            Reel = null;
            flatIndex = -1;
        }

        private async UniTaskVoid LoadAsync(string url, ISpriteCache thumbnailCache, CancellationToken ct)
        {
            view.loadingBrightView.StartLoadingAnimation(view.thumbnailImage.gameObject);
            Sprite? sprite = null;

            try { sprite = await thumbnailCache.GetSpriteAsync(url, useKtx: true, ct: ct); }
            catch (OperationCanceledException) { return; }
            catch (Exception e) { ReportHub.LogException(e, new ReportData(ReportCategory.CAMERA_REEL)); }

            // The cache resolves null instead of throwing when the token is cancelled.
            if (ct.IsCancellationRequested) return;

            view.loadingBrightView.FinishLoadingAnimation(view.thumbnailImage.gameObject);

            if (sprite == null) return;

            Show(sprite.texture, fadeIn: true);
        }

        private void Show(Texture texture, bool fadeIn)
        {
            // The cache keeps textures readable; the gallery never reads pixels back, so the CPU copy is dropped.
            if (texture is Texture2D { isReadable: true } readableTexture)
                readableTexture.Apply(false, true);

            view.ShowTexture(texture, fadeIn);
            imageLoaded = true;
        }

        private void OnPointerEnter()
        {
            CameraReelResponseCompact? reel = Reel;

            if (!imageLoaded || reel == null) return;

            hovered = true;
            view.SetHovered(true);
            optionButton?.TryShow(reel, view.optionButtonContainer.transform, view.optionButtonOffset);
        }

        private void OnPointerExit()
        {
            if (!imageLoaded) return;

            if (optionButton == null)
            {
                ToNormal();
                return;
            }

            if (optionButton.IsContextMenuOpen()) return;

            optionButton.HideControl();
        }

        private void OnClick()
        {
            if (imageLoaded)
                clicked(flatIndex);
        }

        private void ToNormal()
        {
            if (!hovered) return;

            hovered = false;
            view.SetHovered(false);
        }
    }
}
