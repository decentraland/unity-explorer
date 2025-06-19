// SPDX-FileCopyrightText: 2023 Unity Technologies and the KTX for Unity authors
// SPDX-License-Identifier: Apache-2.0


#if !(UNITY_ANDROID || UNITY_WEBGL) || UNITY_EDITOR
#define LOCAL_LOADING
#endif

using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Networking;
using Unity.Collections;

namespace KtxUnity
{
    /// <summary>
    /// Loads a KTX or Basis Universal texture from the StreamingAssets folder, a URL, or a buffer.
    /// </summary>
    public abstract class TextureBase
    {
        /// <summary>
        /// Loads a KTX or Basis Universal texture from the StreamingAssets folder
        /// see https://docs.unity3d.com/Manual/StreamingAssets.html
        /// </summary>
        /// <param name="filePath">Path to the file, relative to StreamingAssets</param>
        /// <param name="linear">Depicts if texture is sampled in linear or
        /// sRGB gamma color space.</param>
        /// <param name="layer">Texture array layer to import</param>
        /// <param name="faceSlice">Cubemap face or 3D/volume texture slice to import.</param>
        /// <param name="mipLevel">Lowest mipmap level to import (where 0 is
        /// the highest resolution). Lower mipmap levels (of higher resolution)
        /// are being discarded. Useful to limit texture resolution.</param>
        /// <param name="mipChain">If true, a mipmap chain (if present) is imported.</param>
        /// <returns>A <see cref="TextureResult"/> that contains an
        /// <see cref="ErrorCode"/>, the resulting texture and its orientation.
        /// </returns>
        public async Task<TextureResult> LoadFromStreamingAssets(
            string filePath,
            bool linear = false,
            uint layer = 0,
            uint faceSlice = 0,
            uint mipLevel = 0,
            bool mipChain = true
            )
        {
            var url = GetStreamingAssetsUrl(filePath);
            return await LoadFile(url, linear, layer, faceSlice, mipLevel, mipChain);
        }

        /// <summary>
        /// Loads a KTX or Basis Universal texture from the StreamingAssets folder
        /// see https://docs.unity3d.com/Manual/StreamingAssets.html
        /// </summary>
        /// <param name="filePath">Path to the file, relative to StreamingAssets</param>
        /// <param name="targetFormat">Desired texture format</param>
        /// <param name="layer">Texture array layer to import</param>
        /// <param name="faceSlice">Cubemap face or 3D/volume texture slice to import.</param>
        /// <param name="mipLevel">Lowest mipmap level to import (where 0 is
        /// the highest resolution). Lower mipmap levels (of higher resolution)
        /// are being discarded. Useful to limit texture resolution.</param>
        /// <param name="mipChain">If true, a mipmap chain (if present) is imported.</param>
        /// <returns>A <see cref="TextureResult"/> that contains an
        /// <see cref="ErrorCode"/>, the resulting texture and its orientation.
        /// </returns>
        public async Task<TextureResult> LoadFromStreamingAssets(
            string filePath,
            GraphicsFormat targetFormat,
            uint layer = 0,
            uint faceSlice = 0,
            uint mipLevel = 0,
            bool mipChain = true
        )
        {
            var url = GetStreamingAssetsUrl(filePath);
            return await LoadFile(url, targetFormat, layer, faceSlice, mipLevel, mipChain);
        }

        /// <summary>
        /// Loads a KTX or Basis Universal texture from a URL
        /// </summary>
        /// <param name="url">URL to the ktx/basis file to load</param>
        /// <param name="linear">Depicts if texture is sampled in linear or
        /// sRGB gamma color space.</param>
        /// <param name="layer">Texture array layer to import</param>
        /// <param name="faceSlice">Cubemap face or 3D/volume texture slice to import.</param>
        /// <param name="mipLevel">Lowest mipmap level to import (where 0 is
        /// the highest resolution). Lower mipmap levels (of higher resolution)
        /// are being discarded. Useful to limit texture resolution.</param>
        /// <param name="mipChain">If true, a mipmap chain (if present) is imported.</param>
        /// <returns>A <see cref="TextureResult"/> that contains an
        /// <see cref="ErrorCode"/>, the resulting texture and its orientation.
        /// </returns>
        public async Task<TextureResult> LoadFromUrl(
            string url,
            bool linear = false,
            uint layer = 0,
            uint faceSlice = 0,
            uint mipLevel = 0,
            bool mipChain = true
            )
        {
            return await LoadFile(url, linear, layer, faceSlice, mipLevel, mipChain);
        }

        /// <summary>
        /// Loads a KTX or Basis Universal texture from a URL
        /// </summary>
        /// <param name="url">URL to the ktx/basis file to load</param>
        /// <param name="targetFormat">Desired texture format</param>
        /// <param name="layer">Texture array layer to import</param>
        /// <param name="faceSlice">Cubemap face or 3D/volume texture slice to import.</param>
        /// <param name="mipLevel">Lowest mipmap level to import (where 0 is
        /// the highest resolution). Lower mipmap levels (of higher resolution)
        /// are being discarded. Useful to limit texture resolution.</param>
        /// <param name="mipChain">If true, a mipmap chain (if present) is imported.</param>
        /// <returns>A <see cref="TextureResult"/> that contains an
        /// <see cref="ErrorCode"/>, the resulting texture and its orientation.
        /// </returns>
        public async Task<TextureResult> LoadFromUrl(
            string url,
            GraphicsFormat targetFormat,
            uint layer = 0,
            uint faceSlice = 0,
            uint mipLevel = 0,
            bool mipChain = true
        )
        {
            return await LoadFile(url, targetFormat, layer, faceSlice, mipLevel, mipChain);
        }

        /// <summary>
        /// Loads a KTX or Basis Universal texture from a buffer
        /// </summary>
        /// <param name="data">Native buffer that holds the ktx/basis file</param>
        /// <param name="linear">Depicts if texture is sampled in linear or
        /// sRGB gamma color space.</param>
        /// <param name="layer">Texture array layer to import</param>
        /// <param name="faceSlice">Cubemap face or 3D/volume texture slice to import.</param>
        /// <param name="mipLevel">Lowest mipmap level to import (where 0 is
        /// the highest resolution). Lower mipmap levels (of higher resolution)
        /// are being discarded. Useful to limit texture resolution.</param>
        /// <param name="mipChain">If true, a mipmap chain (if present) is imported.</param>
        /// <returns>A <see cref="TextureResult"/> that contains an
        /// <see cref="ErrorCode"/>, the resulting texture and its orientation.
        /// </returns>
        public async Task<TextureResult> LoadFromBytes(
            NativeSlice<byte> data,
            bool linear = false,
            uint layer = 0,
            uint faceSlice = 0,
            uint mipLevel = 0,
            bool mipChain = true
            )
        {
            KtxNativeInstance.CertifySupportedPlatform();
            var result = new TextureResult
            {
                errorCode = Open(data)
            };
            if (result.errorCode != ErrorCode.Success) return result;
            result = await LoadTexture2D(linear, layer, faceSlice, mipLevel, mipChain);
            Dispose();
            return result;
        }

        /// <summary>
        /// Loads a KTX or Basis Universal texture from a buffer
        /// </summary>
        /// <param name="data">Native buffer that holds the ktx/basis file</param>
        /// <param name="targetFormat">Desired texture format</param>
        /// <param name="layer">Texture array layer to import</param>
        /// <param name="faceSlice">Cubemap face or 3D/volume texture slice to import.</param>
        /// <param name="mipLevel">Lowest mipmap level to import (where 0 is
        /// the highest resolution). Lower mipmap levels (of higher resolution)
        /// are being discarded. Useful to limit texture resolution.</param>
        /// <param name="mipChain">If true, a mipmap chain (if present) is imported.</param>
        /// <returns>A <see cref="TextureResult"/> that contains an
        /// <see cref="ErrorCode"/>, the resulting texture and its orientation.
        /// </returns>
        // ReSharper disable once MemberCanBePrivate.Global
        public async Task<TextureResult> LoadFromBytes(
            NativeSlice<byte> data,
            GraphicsFormat targetFormat,
            uint layer = 0,
            uint faceSlice = 0,
            uint mipLevel = 0,
            bool mipChain = true
        )
        {
            KtxNativeInstance.CertifySupportedPlatform();
            var result = new TextureResult
            {
                errorCode = Open(data)
            };
            if (result.errorCode != ErrorCode.Success) return result;
            result = await LoadTexture2D(targetFormat, layer, faceSlice, mipLevel, mipChain);
            Dispose();
            return result;
        }

        /// <summary>
        /// Converts a relative sub path within StreamingAssets
        /// and creates an absolute URI from it. Useful for loading
        /// via UnityWebRequests.
        /// </summary>
        /// <param name="subPath">Path, relative to StreamingAssets. Example: path/to/file.ktx</param>
        /// <returns>Platform independent URI that can be loaded via UnityWebRequest</returns>
        public static string GetStreamingAssetsUrl(string subPath)
        {

            var path = Path.Combine(Application.streamingAssetsPath, subPath);

#if LOCAL_LOADING
            path = $"file://{path}";
#endif

            return path;
        }

        #region LowLevelAPI

        /// <summary>
        /// Loads a texture from memory.
        /// Part of the low-level API that provides finer control over the
        /// loading process.
        /// </summary>
        /// <seealso cref="LoadTexture2D(bool,uint,uint,uint,bool)"/>
        /// <seealso cref="LoadTexture2D(GraphicsFormat,uint,uint,uint,bool)"/>
        /// <seealso cref="Dispose"/>
        /// <param name="data">Input texture data</param>
        /// <returns><see cref="ErrorCode.Success"/> if loading was successful
        /// or an error specific code otherwise.</returns>
        public abstract ErrorCode Open(NativeSlice<byte> data);

        /// <summary>
        /// Creates a <see cref="Texture2D"/> from the previously opened
        /// texture.
        /// Transcodes or decodes the texture into a GPU compatible format
        /// (if required) and uploads it to GPU memory.
        /// Part of the low-level API that provides finer control over the
        /// loading process.
        /// </summary>
        /// <seealso cref="Open"/>
        /// <seealso cref="Dispose"/>
        /// <param name="linear">Depicts if texture is sampled in linear or
        /// sRGB gamma color space.</param>
        /// <param name="layer">Texture array layer to import</param>
        /// <param name="faceSlice">Cubemap face or 3D/volume texture slice to import.</param>
        /// <param name="mipLevel">Lowest mipmap level to import (where 0 is
        /// the highest resolution). Lower mipmap levels (of higher resolution)
        /// are being discarded. Useful to limit texture resolution.</param>
        /// <param name="mipChain">If true, a mipmap chain (if present) is imported.</param>
        /// <returns>A <see cref="TextureResult"/> that contains an
        /// <see cref="ErrorCode"/>, the resulting texture and its orientation.
        /// </returns>
        public abstract Task<TextureResult> LoadTexture2D(
            bool linear = false,
            uint layer = 0,
            uint faceSlice = 0,
            uint mipLevel = 0,
            bool mipChain = true
            );

        /// <summary>
        /// Creates a <see cref="Texture2D"/> from the previously opened
        /// texture.
        /// Transcodes or decodes the texture into a desired GPU compatible format
        /// (if required) and uploads it to GPU memory.
        /// Part of the low-level API that provides finer control over the
        /// loading process.
        /// </summary>
        /// <seealso cref="Open"/>
        /// <seealso cref="Dispose"/>
        /// <param name="targetFormat">Desired texture format</param>
        /// <param name="layer">Texture array layer to import</param>
        /// <param name="faceSlice">Cubemap face or 3D/volume texture slice to import.</param>
        /// <param name="mipLevel">Lowest mipmap level to import (where 0 is
        /// the highest resolution). Lower mipmap levels (of higher resolution)
        /// are being discarded. Useful to limit texture resolution.</param>
        /// <param name="mipChain">If true, a mipmap chain (if present) is imported.</param>
        /// <returns>A <see cref="TextureResult"/> that contains an
        /// <see cref="ErrorCode"/>, the resulting texture and its orientation.
        /// </returns>
        public abstract Task<TextureResult> LoadTexture2D(
            GraphicsFormat targetFormat,
            uint layer = 0,
            uint faceSlice = 0,
            uint mipLevel = 0,
            bool mipChain = true
            );

        /// <summary>
        /// Releases all resources.
        /// Part of the low-level API that provides finer control over the
        /// loading process.
        /// </summary>
        /// <seealso cref="Open"/>
        /// <seealso cref="LoadTexture2D(bool,uint,uint,uint,bool)"/>
        /// <seealso cref="LoadTexture2D(GraphicsFormat,uint,uint,uint,bool)"/>
        /// <seealso cref="Dispose"/>
        public abstract void Dispose();

        #endregion

        async Task<TextureResult> LoadFile(
            string url,
            bool linear = false,
            uint layer = 0,
            uint faceSlice = 0,
            uint mipLevel = 0,
            bool mipChain = true
            )
        {
            var webRequest = UnityWebRequest.Get(url);
            var asyncOp = webRequest.SendWebRequest();
            while (!asyncOp.isDone)
            {
                await Task.Yield();
            }

            if (!string.IsNullOrEmpty(webRequest.error))
            {
#if DEBUG
                Debug.LogErrorFormat("Error loading {0}: {1}",url,webRequest.error);
#endif
                return new TextureResult(ErrorCode.OpenUriFailed);
            }

            var buffer = webRequest.downloadHandler.data;

            using (var bufferWrapped = new ManagedNativeArray(buffer))
            {
                return await LoadFromBytes(
                    bufferWrapped.nativeArray,
                    linear,
                    layer,
                    faceSlice,
                    mipLevel,
                    mipChain
                    );
            }
        }

        async Task<TextureResult> LoadFile(
            string url,
            GraphicsFormat targetFormat,
            uint layer = 0,
            uint faceSlice = 0,
            uint mipLevel = 0,
            bool mipChain = true
        )
        {
            var webRequest = UnityWebRequest.Get(url);
            var asyncOp = webRequest.SendWebRequest();
            while (!asyncOp.isDone)
            {
                await Task.Yield();
            }

            if (!string.IsNullOrEmpty(webRequest.error))
            {
#if DEBUG
                Debug.LogErrorFormat("Error loading {0}: {1}",url,webRequest.error);
#endif
                return new TextureResult(ErrorCode.OpenUriFailed);
            }

            var buffer = webRequest.downloadHandler.data;

            using (var bufferWrapped = new ManagedNativeArray(buffer))
            {
                return await LoadFromBytes(
                    bufferWrapped.nativeArray,
                    targetFormat,
                    layer,
                    faceSlice,
                    mipLevel,
                    mipChain
                );
            }
        }

        internal static TranscodeFormatTuple? GetFormat(IMetaData meta, ILevelInfo li, bool linear = false)
        {
            return TranscodeFormatHelper.GetFormatsForImage(meta, li, linear);
        }
    }
}
