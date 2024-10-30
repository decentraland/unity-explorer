using Cysharp.Threading.Tasks;
using DCL.Profiling;
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

        public readonly struct CreateTextureOp : IWebRequestOp<GetTextureWebRequest, IOwnedTexture2D>
        {
            private readonly TextureWrapMode wrapMode;
            private readonly FilterMode filterMode;

            public CreateTextureOp(TextureWrapMode wrapMode, FilterMode filterMode)
            {
                this.wrapMode = wrapMode;
                this.filterMode = filterMode;
            }

            public async UniTask<IOwnedTexture2D?> ExecuteAsync(GetTextureWebRequest webRequest, CancellationToken ct)
            {
                using var request = webRequest.UnityWebRequest;
                var data = request.downloadHandler?.nativeData;

                if (data == null)
                    throw new Exception("Texture content is empty");

                var result = await webRequest.texturesUnzip
                                             .TextureFromBytesAsync(
                                                  AsPointer(data.Value),
                                                  data.Value.Length,
                                                  webRequest.textureType,
                                                  ct
                                              );

                if (result.Success == false)
                    throw new Exception($"CreateTextureOp: Error loading texture url: {webRequest.url} - {result}");

                var texture = result.Value.Texture;

                texture.wrapMode = wrapMode;
                texture.filterMode = filterMode;
                texture.SetDebugName(webRequest.url);
                ProfilingCounters.TexturesAmount.Value++;
                return result.Value;
            }

            private static IntPtr AsPointer<T>(NativeArray<T>.ReadOnly readOnly) where T: struct
            {
                unsafe
                {
                    var ptr = readOnly.GetUnsafeReadOnlyPtr();
                    return new IntPtr(ptr!);
                }
            }
        }
    }
}
