using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using DG.Tweening;
using System;
using Utility;
using System.Threading;
using UnityEngine;

namespace DCL.UI
{
    public class ImageController
    {
        private static readonly Color LOADING_COLOR = new (0, 0, 0, 0);

        private const int PIXELS_PER_UNIT = 50;
        private readonly ImageView view;
        private readonly UITextureProvider? textureProvider;
        private readonly IWebRequestController? webRequestController;
        private readonly Color defaultColor = Color.white;
        private Texture2DRef? currentTextureRef;
        private CancellationTokenSource cts = new();
        public event Action<Sprite>? SpriteLoaded;

        public ImageController(ImageView view, UITextureProvider textureProvider)
        {
            this.view = view;
            this.textureProvider = textureProvider;
            webRequestController = null;
        }
        
        /// <summary>
        /// Use this constructor if the sprite does not need to be cached.
        /// </summary>
        /// <param name="view">The view where the sprite will be presented after loaded.</param>
        /// <param name="webRequestController">The controller to get the sprite from a server.</param>
        public ImageController(ImageView view, IWebRequestController webRequestController)
        {
            this.view = view;
            this.webRequestController = webRequestController;
            textureProvider = null;
        }

        public void RequestImage(string uri, bool removePrevious = false, bool hideImageWhileLoading = false,
            bool useKtx = false, bool fitAndCenterImage = false, Sprite? defaultSprite = null)
        {
            RequestImage(uri, defaultColor, removePrevious, hideImageWhileLoading, useKtx, fitAndCenterImage, defaultSprite);
        }

        public void RequestImage(string uri, Color targetColor, bool removePrevious = false, bool hideImageWhileLoading = false,
            bool useKtx = false, bool fitAndCenterImage = false, Sprite? defaultSprite = null)
        {
            if (removePrevious)
                view.Image.sprite = null;

            if (hideImageWhileLoading)
                view.Image.enabled = false;

            cts = cts.SafeRestart();
            RequestImageAsync(uri, useKtx, targetColor, cts.Token, fitAndCenterImage, defaultSprite).Forget();
        }

        public void SetVisible(bool isVisible)
        {
            view.gameObject.SetActive(isVisible);
        }

        private async UniTask RequestImageAsync(string uri, bool useKtx, Color targetColor, CancellationToken ct, bool fitAndCenterImage = false, Sprite? defaultSprite = null)
        {
            DisposeCurrentTexture();
            
            try
            {
                view.Image.color = LOADING_COLOR;

                view.IsLoading = true;

                Sprite? sprite = null;

                if (textureProvider != null)
                {
                    var textureRef = await textureProvider.LoadTextureAsync(uri, ct);

                    if (textureRef.HasValue)
                    {
                        currentTextureRef = textureRef;

                        sprite = Sprite.Create(
                            textureRef.Value.Texture,
                            new Rect(0, 0, textureRef.Value.Texture.width, textureRef.Value.Texture.height),
                            VectorUtilities.OneHalf,
                            PIXELS_PER_UNIT,
                            0,
                            SpriteMeshType.FullRect,
                            Vector4.one,
                            false
                        );
                    }
                }
                else if (webRequestController != null)
                {
                    // NOTE: This path does NOT cache and does NOT release memory automatically.
                    var ownedTexture = await webRequestController.GetTextureAsync(
                        new CommonArguments(URLAddress.FromString(uri)),
                        new GetTextureArguments(TextureType.Albedo, useKtx),
                        GetTextureWebRequest.CreateTexture(TextureWrapMode.Clamp),
                        ct,
                        ReportCategory.UI
                    );

                    Texture2D? texture = ownedTexture;
                    texture.filterMode = FilterMode.Bilinear;
                    sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), VectorUtilities.OneHalf, PIXELS_PER_UNIT, 0, SpriteMeshType.FullRect, Vector4.one, false);
                }
                else
                {
                    ReportHub.LogError(ReportCategory.UI, "ImageController missing provider configuration.");
                }

                if (sprite != null)
                {
                    SetImage(sprite, fitAndCenterImage);
                    SpriteLoaded?.Invoke(sprite);
                    view.Image.enabled = true;
                    view.Image.DOColor(targetColor, view.imageLoadingFadeDuration);
                }
                else if (defaultSprite != null)
                    TryApplyDefaultSprite(defaultSprite, fitAndCenterImage);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.ENGINE);

                TryApplyDefaultSprite(defaultSprite, fitAndCenterImage);
            }
            finally
            {
                view.IsLoading = false;
                view.Image.enabled = true;

                if (ct.IsCancellationRequested)
                    DisposeCurrentTexture();
            }
        }

        private void DisposeCurrentTexture()
        {
            currentTextureRef?.Dispose();
            currentTextureRef = null;
        }

        private void TryApplyDefaultSprite(Sprite? defaultSprite, bool fitAndCenterImage)
        {
            if (defaultSprite == null) return;

            SetImage(defaultSprite, fitAndCenterImage);
            view.Image.enabled = true;
            view.Image.DOColor(defaultColor, view.imageLoadingFadeDuration);
        }

        public void SetImage(Sprite sprite, bool fitAndCenterImage = false) =>
            view.SetImage(sprite, fitAndCenterImage);

        public void StopLoading()
        {
            cts.SafeCancelAndDispose();
            view.IsLoading = false;
        }
    }
}
