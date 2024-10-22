using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Profiling;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
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
        private readonly ITexturesUnzip texturesUnzip;
        private readonly string url;
        private readonly TextureType textureType;

        private GetTextureWebRequest(UnityWebRequest unityWebRequest, ITexturesUnzip texturesUnzip, string url, TextureType textureType)
        {
            this.url = url;
            this.textureType = textureType;
            this.texturesUnzip = texturesUnzip;
            UnityWebRequest = unityWebRequest;
        }

        public UnityWebRequest UnityWebRequest { get; }

        /// <summary>
        ///     Creates the texture
        /// </summary>
        public static CreateTextureOp CreateTexture(TextureWrapMode wrapMode, FilterMode filterMode = FilterMode.Point) =>
            new (wrapMode, filterMode);

        internal static GetTextureWebRequest Initialize(in CommonArguments commonArguments, GetTextureArguments textureArguments)
        {
            UnityWebRequest wr = UnityWebRequest.Get(commonArguments.URL)!;
            return new GetTextureWebRequest(wr, textureArguments.TexturesUnzip, commonArguments.URL, textureArguments.TextureType);
        }

        public readonly struct CreateTextureOp : IWebRequestOp<GetTextureWebRequest, OwnedTexture2D>
        {
            private readonly TextureWrapMode wrapMode;
            private readonly FilterMode filterMode;

            public CreateTextureOp(TextureWrapMode wrapMode, FilterMode filterMode)
            {
                this.wrapMode = wrapMode;
                this.filterMode = filterMode;
            }

            public async UniTask<OwnedTexture2D?> ExecuteAsync(GetTextureWebRequest webRequest, CancellationToken ct)
            {
                try
                {
                    byte[] data = webRequest.UnityWebRequest.downloadHandler?.data ?? Array.Empty<byte>();

                    var result = await webRequest.texturesUnzip.TextureFromBytesAsync(data, webRequest.textureType, ct).Timeout(TimeSpan.FromSeconds(15));

                    if (result.Success == false)
                    {
                        ReportHub.LogError(ReportCategory.TEXTURES, $"CreateTextureOp: Error loading texture url: {webRequest.url} - {result}");
                        return null;
                    }

                    var texture = result.Value.Texture;

                    texture.wrapMode = wrapMode;
                    texture.filterMode = filterMode;
                    texture.SetDebugName(webRequest.url);
                    ProfilingCounters.TexturesAmount.Value++;
                    return result.Value;
                }
                catch (Exception e)
                {
                    ReportHub.LogException(new Exception($"Error during loading texture from url: {webRequest.url}", e), ReportCategory.TEXTURES);
                    return null;
                }
            }
        }
    }
}
