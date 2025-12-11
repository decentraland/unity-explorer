using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DG.Tweening;
using UnityEngine;
using Utility;

namespace DCL.UI
{
    public class StreamableImageController
    {
        private static readonly Color LOADING_COLOR = new(0, 0, 0, 0);
        private const int PIXELS_PER_UNIT = 50;

        private readonly ImageView view;
        private readonly UITextureProvider textureProvider;
        private readonly Color defaultColor = Color.white;

        private Texture2DRef? currentTextureRef;
        private CancellationTokenSource cts = new();

        public event Action<Sprite>? SpriteLoaded;

        public StreamableImageController(ImageView view, UITextureProvider textureProvider)
        {
            this.view = view;
            this.textureProvider = textureProvider;
        }

        public void RequestImage(string uri, Color targetColor, bool removePrevious = false, bool hideImageWhileLoading = false,
            bool useKtx = false, bool fitAndCenterImage = false, Sprite? defaultSprite = null)
        {
            if (removePrevious)
                view.Image.sprite = null;

            if (hideImageWhileLoading)
                view.Image.enabled = false;

            cts = cts.SafeRestart();

            RequestImageAsync(uri, targetColor, cts.Token, fitAndCenterImage, defaultSprite).Forget();
        }

        private async UniTask RequestImageAsync(string uri, Color targetColor, CancellationToken ct, bool fitAndCenterImage = false, Sprite? defaultSprite = null)
        {
            DisposeCurrentTexture();

            try
            {
                view.Image.color = LOADING_COLOR;
                view.IsLoading = true;
                
                var textureRef = await textureProvider.LoadTextureAsync(uri, ct);

                if (textureRef.HasValue)
                {
                    currentTextureRef = textureRef;
                    
                    var sprite = Sprite.Create(
                        textureRef.Value.Texture,
                        new Rect(0, 0, textureRef.Value.Texture.width, textureRef.Value.Texture.height),
                        VectorUtilities.OneHalf,
                        PIXELS_PER_UNIT,
                        0,
                        SpriteMeshType.FullRect,
                        Vector4.one,
                        false
                    );

                    ApplySprite(sprite, targetColor, fitAndCenterImage);
                }
                else if (defaultSprite != null)
                {
                    TryApplyDefaultSprite(defaultSprite, fitAndCenterImage);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.UI);
                TryApplyDefaultSprite(defaultSprite, fitAndCenterImage);
            }
            finally
            {
                view.IsLoading = false;
                if (ct.IsCancellationRequested) DisposeCurrentTexture();
            }
        }

        private void DisposeCurrentTexture()
        {
            currentTextureRef?.Dispose();
            currentTextureRef = null;
        }

        private void ApplySprite(Sprite sprite, Color targetColor, bool fitAndCenterImage)
        {
            view.SetImage(sprite, fitAndCenterImage);
            SpriteLoaded?.Invoke(sprite);
            view.Image.enabled = true;
            view.Image.DOColor(targetColor, view.imageLoadingFadeDuration);
        }

        private void TryApplyDefaultSprite(Sprite? defaultSprite, bool fitAndCenterImage)
        {
            if (defaultSprite == null) return;
            view.SetImage(defaultSprite, fitAndCenterImage);
            view.Image.enabled = true;
            view.Image.DOColor(defaultColor, view.imageLoadingFadeDuration);
        }

        public void StopLoading()
        {
            cts.SafeCancelAndDispose();
            DisposeCurrentTexture();
            view.IsLoading = false;
        }

        public void RequestImage(string uri, bool removePrevious = false, bool hideImageWhileLoading = false,
            bool useKtx = false, bool fitAndCenterImage = false, Sprite? defaultSprite = null)
        {
            RequestImage(uri, defaultColor, removePrevious, hideImageWhileLoading, useKtx, fitAndCenterImage, defaultSprite);
        }

        public void SetImage(Sprite sprite, bool fitAndCenterImage = false)
        {
            DisposeCurrentTexture();
            view.SetImage(sprite, fitAndCenterImage);
        }
    }
}