using Cysharp.Threading.Tasks;
using DCL.Profiling;
using KtxUnity;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using System;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
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
        private readonly bool ktxEnabled;

        private GetTextureWebRequest(UnityWebRequest unityWebRequest, string url, bool ktxEnabled)
        {
            this.url = url;
            this.ktxEnabled = ktxEnabled;
            UnityWebRequest = unityWebRequest;
        }

        public UnityWebRequest UnityWebRequest { get; }

        /// <summary>
        ///     Creates the texture
        /// </summary>
        public static CreateTextureOp CreateTexture(TextureWrapMode wrapMode, FilterMode filterMode = FilterMode.Point) =>
            new (wrapMode, filterMode);

        internal static GetTextureWebRequest Initialize(in CommonArguments commonArguments, GetTextureArguments textureArguments, bool ktxEnabled)
        {
            // TODO mihak: Unhardcode this
            var convertUrl = $"https://media-opticonverter.decentraland.zone/convert?ktx2={ktxEnabled}&fileUrl={Uri.EscapeDataString(commonArguments.URL)}";
            UnityWebRequest wr = UnityWebRequest.Get(convertUrl);
            return new GetTextureWebRequest(wr, convertUrl, ktxEnabled);
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
                return webRequest.ktxEnabled ? ExecuteKtxAsync(webRequest, ct) : ExecuteNoCompressionAsync(webRequest, ct);
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
                return UniTask.FromResult((IOwnedTexture2D?)new IOwnedTexture2D.Const(texture));
            }

            private async UniTask<IOwnedTexture2D?> ExecuteKtxAsync(GetTextureWebRequest webRequest, CancellationToken ct)
            {
                var ktxTexture = new KtxTexture();

                using var bufferWrapped = new ManagedNativeArray(webRequest.UnityWebRequest.downloadHandler.data);

                var result = await ktxTexture.LoadFromBytes(
                    bufferWrapped.nativeArray,
                    false,
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
