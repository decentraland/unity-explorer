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

        public bool Idempotent => true;

        /// <summary>
        ///     Creates the texture
        /// </summary>
        public static CreateTextureOp CreateTexture(TextureWrapMode wrapMode, FilterMode filterMode = FilterMode.Point) =>
            new (wrapMode, filterMode);

        internal static GetTextureWebRequest Initialize(string url, GetTextureArguments textureArguments, IDecentralandUrlsSource urlsSource, bool ktxEnabled)
        {
            bool useKtx = textureArguments.UseKtx && ktxEnabled && !WebRequestUtils.IsLocalhost(url);
            string requestUrl = useKtx ? string.Format(urlsSource.Url(DecentralandUrl.MediaConverter), Uri.EscapeDataString(url)) : url;
            UnityWebRequest webRequest = UnityWebRequest.Get(requestUrl);

            return new GetTextureWebRequest(webRequest, requestUrl, textureArguments.TextureType);
        }

        public readonly struct CreateTextureOp : IWebRequestOp<GetTextureWebRequest, Texture2D>
        {
            private readonly TextureWrapMode wrapMode;
            private readonly FilterMode filterMode;

            public CreateTextureOp(TextureWrapMode wrapMode, FilterMode filterMode)
            {
                this.wrapMode = wrapMode;
                this.filterMode = filterMode;
            }

            public UniTask<Texture2D?> ExecuteAsync(GetTextureWebRequest webRequest, CancellationToken ct)
            {
                string? contentType = webRequest.UnityWebRequest.GetResponseHeader("Content-Type");

                return contentType == "image/ktx2" ? ExecuteKtxAsync(webRequest, ct) : ExecuteNoCompressionAsync(webRequest, ct);
            }

            private UniTask<Texture2D> ExecuteNoCompressionAsync(GetTextureWebRequest webRequest, CancellationToken ct)
            {
                Texture2D texture;

                if (webRequest.UnityWebRequest.downloadHandler is DownloadHandlerTexture) { texture = DownloadHandlerTexture.GetContent(webRequest.UnityWebRequest); }
                else
                {
                    // If there's no DownloadHandlerTexture the texture needs to be created from scratch with the
                    // downloaded tex data
                    var data = webRequest.UnityWebRequest.downloadHandler?.data;

                    if (data == null)
                        throw new Exception("Texture content is empty");

                    texture = new Texture2D(2, 2, TextureFormat.RGBA32, false,
                        linear: webRequest.textureType == TextureType.NormalMap);

                    if (!texture.LoadImage(data)) { throw new Exception($"Failed to load image from data: {webRequest.url}"); }
                }

                texture.wrapMode = wrapMode;
                texture.filterMode = filterMode;
                texture.SetDebugName(webRequest.url);
                ProfilingCounters.TexturesAmount.Value++;
                return UniTask.FromResult(texture);
            }

            private async UniTask<Texture2D> ExecuteKtxAsync(GetTextureWebRequest webRequest, CancellationToken ct)
            {
                // TODO: .data creates an array
                var data = webRequest.UnityWebRequest.downloadHandler?.data;

                if (data == null)
                    throw new Exception($"Texture content is empty: {webRequest.url}");

                using var bufferWrapped = new ManagedNativeArray(data);

                var ktxTexture = new KtxTexture();

                // Open() can throw before allocating native state; keep it outside the try so Dispose only runs once that state exists.
                var openResult = ktxTexture.Open(bufferWrapped.nativeArray.AsReadOnly());

                try
                {
                    if (openResult != ErrorCode.Success)
                        throw new Exception($"Failed to open ktx texture from data ({openResult}): {webRequest.url}");

                    // readable: true keeps the decoded texture CPU-readable.
                    var result = await ktxTexture.LoadTexture2D(
                        webRequest.textureType != TextureType.Albedo, // BaseColour or any colour image should be non-linear; Metallic-roughness, normals or any data based textures should be linear
                        readable: true
                    );

                    if (result.errorCode != ErrorCode.Success)
                    {
                        // LoadTexture2D can allocate the Texture2D before failing (e.g. Apply/upload throws); destroy it so it doesn't leak.
                        UnityObjectUtils.SafeDestroy(result.texture);
                        throw new Exception($"Failed to load ktx texture from data ({result.errorCode}): {webRequest.url}");
                    }

                    var finalTex = result.texture;

                    finalTex.wrapMode = wrapMode;
                    finalTex.filterMode = filterMode;
                    finalTex.SetDebugName(webRequest.url);
                    ProfilingCounters.TexturesAmount.Value++;
                    return finalTex;
                }
                finally
                {
                    ktxTexture.Dispose();
                }
            }
        }
    }
}
