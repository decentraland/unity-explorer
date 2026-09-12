using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using JetBrains.Annotations;
using SceneRunner.Scene;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace SceneRuntime.Apis.Modules.AdaptationLayerHelper
{
    public class AdaptationLayerHelperWrapper : JsApiWrapper
    {
        private readonly IWebRequestController webRequestController;
        private readonly ISceneData sceneData;

        public AdaptationLayerHelperWrapper(IWebRequestController webRequestController, ISceneData sceneData, CancellationTokenSource disposeCts) : base(disposeCts)
        {
            this.webRequestController = webRequestController;
            this.sceneData = sceneData;
        }

        [PublicAPI("Used by StreamingAssets/Js/Modules/AdaptationLayerHelper.js")]
        public object GetTextureSize(string src)
        {
            return GetTextureSizeAsync(disposeCts.Token).ToDisconnectedPromise(this);

            async UniTask<TextureSizeToJs> GetTextureSizeAsync(CancellationToken ct)
            {
                if (!TryResolveTextureUrl(sceneData, src, out URLAddress url))
                    throw new ArgumentException($"Texture '{src}' is neither scene content nor an allowed media URL");

                await UniTask.SwitchToMainThread();

                // Fetch the original file rather than the KTX variant: converted textures may not keep the source dimensions.
                Texture2D texture = await webRequestController.GetTextureAsync(
                    new CommonArguments(url),
                    new GetTextureArguments(TextureType.Albedo, useKtx: false),
                    GetTextureWebRequest.CreateTexture(TextureWrapMode.Clamp),
                    ct,
                    ReportCategory.SCENE_UI);

                try { return new TextureSizeToJs { width = texture.width, height = texture.height }; }
                finally { UnityObjectUtils.SafeDestroy(texture); }
            }
        }

        internal static bool TryResolveTextureUrl(ISceneData sceneData, string? src, out URLAddress url)
        {
            url = URLAddress.EMPTY;
            return src != null && !string.IsNullOrWhiteSpace(src) && sceneData.TryGetMediaUrl(src, out url);
        }

        [PublicAPI]
        public struct TextureSizeToJs
        {
            public int width;
            public int height;
        }
    }
}
