using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using System;
using Utility;
using System.Threading;
using UnityEngine;

namespace DCL.UI
{
    public class ImageController
    {
        private const int PIXELS_PER_UNIT = 50;
        private readonly ImageView view;
        private readonly IWebRequestController webRequestController;
        private CancellationTokenSource cts;
        public event Action<Sprite>? SpriteLoaded;

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

            cts.SafeCancelAndDispose();
            cts = new CancellationTokenSource();
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
                view.LoadingObject.SetActive(true);

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
                Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), VectorUtilities.OneHalf, PIXELS_PER_UNIT, 0, SpriteMeshType.FullRect, Vector4.one, false);
                SetImage(sprite);
                SpriteLoaded?.Invoke(sprite);
                view.Image.enabled = true;
            }
            catch (Exception e)
            {
                view.LoadingObject.SetActive(false);
                view.Image.enabled = true;
                throw;
            }
        }

        public void SetImage(Sprite sprite)
        {
            view.Image.sprite = sprite;
            view.LoadingObject.SetActive(false);
        }

        public void StopLoading()
        {
            cts.SafeCancelAndDispose();
            view.LoadingObject.SetActive(false);
        }
    }
}
