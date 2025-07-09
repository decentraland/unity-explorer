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
        private readonly IWebRequestController? webRequestController;
        private CancellationTokenSource cts = new();
        public event Action<Sprite>? SpriteLoaded;

        /// <summary>
        /// Use this constructor if the sprite does not need to be cached.
        /// </summary>
        /// <param name="view">The view where the sprite will be presented after loaded.</param>
        /// <param name="webRequestController">The controller to get the sprite from a server.</param>
        public ImageController(ImageView view, IWebRequestController webRequestController)
        {
            this.view = view;
            this.webRequestController = webRequestController;
        }

        public void RequestImage(string uri, bool removePrevious = false, bool hideImageWhileLoading = false, bool useKtx = false)
        {
            if (removePrevious)
                view.Image.sprite = null;

            if (hideImageWhileLoading)
                view.Image.enabled = false;

            cts = cts.SafeRestart();
            RequestImageAsync(uri, useKtx, cts.Token).Forget();
        }

        public void SetVisible(bool isVisible)
        {
            view.gameObject.SetActive(isVisible);
        }

        public async UniTask RequestImageAsync(string uri, bool useKtx, CancellationToken ct)
        {
            try
            {
                view.Image.color = LOADING_COLOR;

                view.LoadingObject.SetActive(true);

                Sprite? sprite = null;

                if (webRequestController != null)
                {
                    //TODO potential memory leak, due no CacheCleaner
                    IOwnedTexture2D ownedTexture = await webRequestController.GetTextureAsync(
                        new CommonArguments(URLAddress.FromString(uri)),
                        new GetTextureArguments(TextureType.Albedo, useKtx),
                        GetTextureWebRequest.CreateTexture(TextureWrapMode.Clamp),
                        ct,
                        ReportCategory.UI
                    );

                    var texture = ownedTexture.Texture;
                    texture.filterMode = FilterMode.Bilinear;
                    sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), VectorUtilities.OneHalf, PIXELS_PER_UNIT, 0, SpriteMeshType.FullRect, Vector4.one, false);
                }
                else
                {
                    ReportHub.LogError(ReportCategory.UI, "The image controller was not configured properly. It requires either a web request controller or a sprite cache.");
                }

                if (sprite != null)
                {
                    SetImage(sprite);
                    SpriteLoaded?.Invoke(sprite);
                    view.Image.enabled = true;
                    view.Image.DOColor(Color.white, view.imageLoadingFadeDuration);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.ENGINE);
            }
            finally
            {
                view.LoadingObject.SetActive(false);
                view.Image.enabled = true;
            }
        }

        public void SetImage(Sprite sprite)
        {
            view.SetImage(sprite);
        }

        public void StopLoading()
        {
            cts.SafeCancelAndDispose();
            view.LoadingObject.SetActive(false);
        }
    }
}
