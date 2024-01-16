using DCL.Profiling;
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

        public UnityWebRequest UnityWebRequest { get; }

        private GetTextureWebRequest(UnityWebRequest unityWebRequest, string url)
        {
            this.url = url;
            UnityWebRequest = unityWebRequest;
        }

        /// <summary>
        ///     Creates the texture and finalizes the request
        /// </summary>
        /// <param name="wrapMode"></param>
        /// <param name="filterMode"></param>
        /// <returns></returns>
        public Texture2D CreateTexture(TextureWrapMode wrapMode, FilterMode filterMode = FilterMode.Point)
        {
            Texture2D tex = DownloadHandlerTexture.GetContent(UnityWebRequest);
            tex.wrapMode = wrapMode;
            tex.filterMode = filterMode;

            UnityWebRequest.Dispose();

            tex.SetDebugName(url);
            ProfilingCounters.TexturesAmount.Value++;

            return tex;
        }

        internal static GetTextureWebRequest Initialize(in CommonArguments commonArguments, GetTextureArguments textureArguments)
        {
            UnityWebRequest wr = UnityWebRequestTexture.GetTexture(commonArguments.URL, !textureArguments.IsReadable);
            return new GetTextureWebRequest(wr, commonArguments.URL);
        }
    }
}
