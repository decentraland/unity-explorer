using Cysharp.Threading.Tasks;
using DCL.Profiling;
using KtxUnity;
using System;
using UnityEngine;
using Utility;

namespace DCL.WebRequests
{
    /// <summary>
    ///     Decodes a KTX2 file into a <see cref="Texture2D" /> through the ktx_unity native plugin, transcoding it to the GPU format of the platform.
    /// </summary>
    public static class KtxTextureDecoder
    {
        /// <param name="data">The KTX2 file.</param>
        /// <param name="linear">False for colour images; true for data textures such as normals or metallic-roughness.</param>
        /// <param name="readable">Whether the texture keeps a CPU copy of its pixels.</param>
        /// <param name="debugName">The name the texture reports in profilers, usually its URL.</param>
        public static async UniTask<Texture2D> DecodeAsync(byte[] data, bool linear, TextureWrapMode wrapMode, FilterMode filterMode, bool readable, string debugName)
        {
            using var bufferWrapped = new ManagedNativeArray(data);

            var ktxTexture = new KtxTexture();

            // Open() can throw before allocating native state; keep it outside the try/finally so Dispose only runs once that state exists.
            ErrorCode openResult;

            try { openResult = ktxTexture.Open(bufferWrapped.nativeArray.AsReadOnly()); }
            catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
            {
                // The OS failing to open the native plugin (or resolve a symbol in it) is per-machine-permanent, not transient.
                KtxNativeSupport.MarkUnsupported();
                throw;
            }

            try
            {
                if (openResult != ErrorCode.Success)
                    throw new Exception($"Failed to open ktx texture from data ({openResult}): {debugName}");

                var result = await ktxTexture.LoadTexture2D(linear, readable: readable);

                if (result.errorCode != ErrorCode.Success)
                {
                    // LoadTexture2D can allocate the Texture2D before failing (e.g. Apply/upload throws); destroy it so it doesn't leak.
                    UnityObjectUtils.SafeDestroy(result.texture);
                    throw new Exception($"Failed to load ktx texture from data ({result.errorCode}): {debugName}");
                }

                Texture2D texture = result.texture;

                texture.wrapMode = wrapMode;
                texture.filterMode = filterMode;
                texture.SetDebugName(debugName);
                ProfilingCounters.TexturesAmount.Value++;
                return texture;
            }
            finally
            {
                ktxTexture.Dispose();
            }
        }
    }
}
