using Cysharp.Threading.Tasks;
using DCL.Profiling;
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

        private GetTextureWebRequest(UnityWebRequest unityWebRequest, string url)
        {
            this.url = url;
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
            UnityWebRequest wr = UnityWebRequestTexture.GetTexture(commonArguments.URL, !textureArguments.IsReadable);
            return new GetTextureWebRequest(wr, commonArguments.URL);
        }

        public struct CreateTextureOp : IWebRequestOp<GetTextureWebRequest, Texture2D>
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
                var texture = DownloadHandlerTexture.GetContent(webRequest.UnityWebRequest);
                texture.wrapMode = wrapMode;
                texture.filterMode = filterMode;
                texture.SetDebugName(webRequest.url);
                ProfilingCounters.TexturesAmount.Value++;
                return UniTask.FromResult(texture)!;
            }
        }
    }
}
