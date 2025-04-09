using Cysharp.Threading.Tasks;
using DCL.Profiling;
using Plugins.TexturesFuse.TexturesServerWrap;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using System;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Networking;
using Utility;
using Utility.Types;

namespace DCL.WebRequests
{
    /// <summary>
    ///     Representation of the created web request dedicated to download a texture
    /// </summary>
    public class GetTextureWebRequest : TypedWebRequestBase<GetTextureArguments>
    {
        private readonly ITexturesFuse texturesFuse;
        private readonly bool isTextureCompressionEnabled;

        internal GetTextureWebRequest(RequestEnvelope envelope, GetTextureArguments args, IWebRequestController controller, ITexturesFuse texturesFuse, bool isTextureCompressionEnabled)
            : base(envelope, args, controller)
        {
            this.texturesFuse = texturesFuse;
            this.isTextureCompressionEnabled = isTextureCompressionEnabled;
        }

        public override bool Http2Supported => false;

        /// <summary>
        ///     Creates the texture
        /// </summary>
        public async UniTask<IOwnedTexture2D> CreateTextureAsync(TextureWrapMode wrapMode, FilterMode filterMode = FilterMode.Point, CancellationToken ct = default)
        {
            using GetTextureWebRequest _ = this;
            using IWebRequest wr = await this.SendAsync(ct);

            // For simplicity simply switch

            switch (wr.nativeRequest)
            {
                case UnityWebRequest unityWebRequest:
                    return await (isTextureCompressionEnabled ? ExecuteWithCompressionAsync(unityWebRequest, wrapMode, filterMode, ct) : ExecuteNoCompressionAsync(unityWebRequest, wrapMode, filterMode, ct));

                default:
                    throw new NotSupportedException($"{nameof(CreateTextureAsync)} does not support {wr.GetType().Name})");
            }
        }

        private UniTask<IOwnedTexture2D> ExecuteNoCompressionAsync(UnityWebRequest webRequest, TextureWrapMode wrapMode, FilterMode filterMode, CancellationToken ct)
        {
            Texture2D? texture;

            if (webRequest.downloadHandler is DownloadHandlerTexture) { texture = DownloadHandlerTexture.GetContent(webRequest); }
            else
            {
                // If there's no DownloadHandlerTexture the texture needs to be created from scratch with the
                // downloaded tex data
                byte[]? data = webRequest.downloadHandler?.data;

                if (data == null)
                    throw new Exception("Texture content is empty");

                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);

                if (!texture.LoadImage(data)) { throw new Exception($"Failed to load image from data: {webRequest.url}"); }
            }

            texture.wrapMode = wrapMode;
            texture.filterMode = filterMode;
            texture.SetDebugName(webRequest.url);
            ProfilingCounters.TexturesAmount.Value++;
            return UniTask.FromResult((IOwnedTexture2D)new IOwnedTexture2D.Const(texture));
        }

        private async UniTask<IOwnedTexture2D> ExecuteWithCompressionAsync(UnityWebRequest request, TextureWrapMode wrapMode, FilterMode filterMode, CancellationToken ct)
        {
            NativeArray<byte>.ReadOnly? data = request.downloadHandler?.nativeData;

            if (data == null)
                throw new Exception("Texture content is empty");

            EnumResult<IOwnedTexture2D, NativeMethods.ImageResult> result = await texturesFuse
               .TextureFromBytesAsync(
                    AsPointer(data.Value),
                    data.Value.Length,
                    Args.TextureType,
                    ct
                );

            // Fallback to uncompressed texture if compression fails
            if (result.Success == false)
                return await ExecuteNoCompressionAsync(request, wrapMode, filterMode, ct);

            Texture2D texture = result.Value.Texture;

            texture.wrapMode = wrapMode;
            texture.filterMode = filterMode;
            texture.SetDebugName(Envelope.CommonArguments.URL);
            ProfilingCounters.TexturesAmount.Value++;
            return result.Value;
        }

        private static IntPtr AsPointer<T>(NativeArray<T>.ReadOnly readOnly) where T: struct
        {
            unsafe
            {
                void* ptr = readOnly.GetUnsafeReadOnlyPtr();
                return new IntPtr(ptr!);
            }
        }
    }
}
