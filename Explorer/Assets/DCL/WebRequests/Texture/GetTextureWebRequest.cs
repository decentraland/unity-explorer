using Cysharp.Threading.Tasks;
using DCL.Profiling;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
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

        private GetTextureWebRequest(UnityWebRequest unityWebRequest, ITexturesFuse texturesFuse, string url, TextureType textureType)
        {
            this.url = url;
            this.textureType = textureType;
            this.texturesFuse = texturesFuse;
            UnityWebRequest = unityWebRequest;
        }

        public UnityWebRequest UnityWebRequest { get; }

        /// <summary>
        ///     Creates the texture
        /// </summary>
        public static CreateTextureOp CreateTexture(TextureWrapMode wrapMode, FilterMode filterMode = FilterMode.Point) =>
            new (wrapMode, filterMode);

        internal static GetTextureWebRequest Initialize(in CommonArguments commonArguments, GetTextureArguments textureArguments, ITexturesFuse texturesFuse)
        {
            UnityWebRequest wr = UnityWebRequest.Get(commonArguments.URL)!;
            return new GetTextureWebRequest(wr, texturesFuse, commonArguments.URL, textureArguments.TextureType);
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

            private const string CACHE_LOCATION = "/Users/nickkhalow/Downloads/CacheTest";
            private static readonly StreamWriter MAPPING = new (Path.Combine(CACHE_LOCATION, "mapping.txt"), true);

            static CreateTextureOp()
            {
                MAPPING.AutoFlush = true;
            }

            public async UniTask<IOwnedTexture2D?> ExecuteAsync(GetTextureWebRequest webRequest, CancellationToken ct)
            {
                var sha512 = SHA512.Create()!;
                byte[] inputHashData = Encoding.UTF8.GetBytes(webRequest.url);
                byte[] hashBytes = sha512.ComputeHash(inputHashData);
                var hash = BitConverter.ToString(hashBytes);

                using var request = webRequest.UnityWebRequest;

                string path = Path.Combine(CACHE_LOCATION, hash);

                if (webRequest.url.EndsWith(".png", StringComparison.Ordinal))
                    path = Path.ChangeExtension(path, ".png");
                else if (webRequest.url.EndsWith(".jpg", StringComparison.Ordinal))
                    path = Path.ChangeExtension(path, ".jpg");

                byte[] data = Array.Empty<byte>();

                if (File.Exists(path))
                    data = await File.ReadAllBytesAsync(path, ct)!;
                else
                {
                    data = request.downloadHandler!.data!;
                    await File.WriteAllBytesAsync(path, data, ct)!;
                    await MAPPING.WriteLineAsync($"{webRequest.url} -> {path}")!;
                }

                if (data == null)
                    throw new Exception("Texture content is empty");

                // var imageData = new ITexturesFuse.ImageData(data, webRequest.textureType, webRequest.url);
                var result = await webRequest.texturesFuse.TextureFromBytesAsync(data, webRequest.textureType, ct, webRequest.url);

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
