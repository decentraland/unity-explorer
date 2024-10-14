using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using DCL.WebRequests.ArgsFactory;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
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
        private readonly IGetTextureArgsFactory getTextureArgsFactory;
        private CancellationTokenSource cts;

        public ImageController(ImageView view, IWebRequestController webRequestController, IGetTextureArgsFactory getTextureArgsFactory)
        {
            this.view = view;
            this.webRequestController = webRequestController;
            this.getTextureArgsFactory = getTextureArgsFactory;
        }

        public void RequestImage(string uri, bool removePrevious = false, bool hideImageWhileLoading = false)
        {
            if (removePrevious)
                view.Image.sprite = null;

            if (hideImageWhileLoading)
                view.Image.enabled = false;

            cts.SafeCancelAndDispose();
            cts = new CancellationTokenSource();
            RequestImageAsync(uri, cts.Token).Forget();
        }

        public void SetVisible(bool isVisible)
        {
            view.gameObject.SetActive(isVisible);
        }

        public async UniTask RequestImageAsync(string uri, CancellationToken ct)
        {
            try
            {
                view.LoadingObject.SetActive(true);

                //TODO potential memory leak, due no CacheCleaner
                OwnedTexture2D ownedTexture = await webRequestController.GetTextureAsync(
                    new CommonArguments(URLAddress.FromString(uri)),
                    getTextureArgsFactory.NewArguments(),
                    GetTextureWebRequest.CreateTexture(TextureWrapMode.Clamp),
                    ct,
                    ReportCategory.UI
                );

                var texture = ownedTexture.Texture;
                texture.filterMode = FilterMode.Bilinear;
                view.Image.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), VectorUtilities.OneHalf, PIXELS_PER_UNIT, 0, SpriteMeshType.FullRect, Vector4.one, false);
                view.LoadingObject.SetActive(false);
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
