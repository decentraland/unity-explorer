using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Profiling;
using KtxUnity;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
using Utility;

namespace DCL.WebRequests
{
    /// <summary>
    ///     Representation of the created web request dedicated to download a texture
    /// </summary>
    public readonly struct GetTextureWebRequest : ITypedWebRequest
    {
        private readonly string url;
        private readonly TextureType textureType;

        private GetTextureWebRequest(UnityWebRequest unityWebRequest, string url, TextureType textureType)
        {
            this.url = url;
            this.textureType = textureType;
            UnityWebRequest = unityWebRequest;
        }

        public UnityWebRequest UnityWebRequest { get; }

        /// <summary>
        ///     Creates the texture
        /// </summary>
        public static CreateTextureOp CreateTexture(TextureWrapMode wrapMode, FilterMode filterMode = FilterMode.Point) =>
            new (wrapMode, filterMode);

        internal static GetTextureWebRequest Initialize(in CommonArguments commonArguments, GetTextureArguments textureArguments, IDecentralandUrlsSource urlsSource, bool ktxEnabled)
        {
            bool useKtx = textureArguments.UseKtx && ktxEnabled;
            string requestUrl = useKtx ? string.Format(urlsSource.Url(DecentralandUrl.MediaConverter), Uri.EscapeDataString(commonArguments.URL)) : commonArguments.URL;
            UnityWebRequest webRequest = UnityWebRequest.Get(requestUrl);

            return new GetTextureWebRequest(webRequest, requestUrl, textureArguments.TextureType);
        }

        public readonly struct CreateTextureOp : IWebRequestOp<GetTextureWebRequest, IOwnedTexture2D>
        {
            private readonly TextureWrapMode wrapMode;
            private readonly FilterMode filterMode;

            public CreateTextureOp(TextureWrapMode wrapMode, FilterMode filterMode)
            {
                this.wrapMode = wrapMode;
                this.filterMode = filterMode;
            }

            public UniTask<IOwnedTexture2D?> ExecuteAsync(GetTextureWebRequest webRequest, CancellationToken ct)
            {
                string? contentType = webRequest.UnityWebRequest.GetResponseHeader("Content-Type");

                return contentType == "image/ktx2" ? ExecuteKtxAsync(webRequest, ct) : ExecuteNoCompressionAsync(webRequest, ct);
            }

            private UniTask<IOwnedTexture2D?> ExecuteNoCompressionAsync(GetTextureWebRequest webRequest, CancellationToken ct)
            {
                Texture2D? texture;

                if (webRequest.UnityWebRequest.downloadHandler is DownloadHandlerTexture) { texture = DownloadHandlerTexture.GetContent(webRequest.UnityWebRequest); }
                else
                {
                    // If there's no DownloadHandlerTexture the texture needs to be created from scratch with the
                    // downloaded tex data
                    var data = webRequest.UnityWebRequest.downloadHandler?.data;

                    if (data == null)
                        throw new Exception("Texture content is empty");

                    texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);

                    if (!texture.LoadImage(data)) { throw new Exception($"Failed to load image from data: {webRequest.url}"); }
                }

                texture.wrapMode = wrapMode;
                texture.filterMode = filterMode;
                texture.SetDebugName(webRequest.url);
                ProfilingCounters.TexturesAmount.Value++;
                return UniTask.FromResult<IOwnedTexture2D?>(new IOwnedTexture2D.Const(texture));
            }

            private async UniTask<IOwnedTexture2D?> ExecuteKtxAsync(GetTextureWebRequest webRequest, CancellationToken ct)
            {
                var ktxTexture = new KtxTexture();

                // TODO: .data creates an array
                using var bufferWrapped = new ManagedNativeArray(webRequest.UnityWebRequest.downloadHandler.data);

                var result = await ktxTexture.LoadFromBytes(
                    bufferWrapped.nativeArray,
                    webRequest.textureType != TextureType.Albedo, // BaseColour or any colour image should be non-linear; Metallic-roughness, normals or any data based textures should be linear
                    0,
                    0,
                    0,
                    true
                );

                if (result == null)
                    throw new Exception($"Failed to load ktx texture from data: {webRequest.url}");

                var finalTex = result.texture;

                finalTex.wrapMode = wrapMode;
                finalTex.filterMode = filterMode;
                finalTex.SetDebugName(webRequest.url);
                ProfilingCounters.TexturesAmount.Value++;
                return new IOwnedTexture2D.Const(finalTex);
            }
        }
    }
}
