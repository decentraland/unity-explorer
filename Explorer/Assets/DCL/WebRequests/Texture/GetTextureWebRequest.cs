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
        private readonly ITexturesFuse texturesFuse;
        private readonly string url;
        private readonly TextureType textureType;
        private readonly bool isTextureCompressionEnabled;

        private GetTextureWebRequest(UnityWebRequest unityWebRequest, ITexturesFuse texturesFuse, string url, TextureType textureType, bool isTextureCompressionEnabled)
        {
            this.url = url;
            this.textureType = textureType;
            this.isTextureCompressionEnabled = isTextureCompressionEnabled;
            this.texturesFuse = texturesFuse;
            UnityWebRequest = unityWebRequest;
        }

        public UnityWebRequest UnityWebRequest { get; }

        /// <summary>
        ///     Creates the texture
        /// </summary>
        public static CreateTextureOp CreateTexture(TextureWrapMode wrapMode, FilterMode filterMode = FilterMode.Point) =>
            new (wrapMode, filterMode);

        internal static GetTextureWebRequest Initialize(in CommonArguments commonArguments, GetTextureArguments textureArguments, ITexturesFuse texturesFuse, bool isTextureCompressionEnabled)
        {
            UnityWebRequest wr = isTextureCompressionEnabled ? UnityWebRequest.Get(commonArguments.URL)! : UnityWebRequestTexture.GetTexture(commonArguments.URL, false);
            return new GetTextureWebRequest(wr, texturesFuse, commonArguments.URL, textureArguments.TextureType, isTextureCompressionEnabled);
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
                if (webRequest.isTextureCompressionEnabled)
                    return ExecuteWithCompressionAsync(webRequest, ct);

                return ExecuteNoCompressionAsync(webRequest, ct)!;
            }

            private UniTask<IOwnedTexture2D> ExecuteNoCompressionAsync(GetTextureWebRequest webRequest, CancellationToken ct)
            {
                Texture2D? texture = DownloadHandlerTexture.GetContent(webRequest.UnityWebRequest);
                texture.wrapMode = wrapMode;
                texture.filterMode = filterMode;
                texture.SetDebugName(webRequest.url);
                ProfilingCounters.TexturesAmount.Value++;
                return UniTask.FromResult((IOwnedTexture2D)new IOwnedTexture2D.Const(texture));
            }

            private async UniTask<IOwnedTexture2D?> ExecuteWithCompressionAsync(GetTextureWebRequest webRequest, CancellationToken ct)
            {
                using var request = webRequest.UnityWebRequest;
                var data = request.downloadHandler?.nativeData;

                if (data == null)
                    throw new Exception("Texture content is empty");

                var result = await webRequest.texturesFuse
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
