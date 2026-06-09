// SPDX-FileCopyrightText: 2023 Unity Technologies and the KTX for Unity authors
// SPDX-License-Identifier: Apache-2.0


using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Profiling;
using Unity.Collections;
using UnityEngine.Assertions;

namespace KtxUnity
{
    /// <summary>
    /// Loads a KTX texture from the StreamingAssets folder, a URL, or a buffer.
    /// </summary>
    public class KtxTexture : TextureBase
    {

        KtxNativeInstance m_Ktx;

        // ReSharper disable MemberCanBePrivate.Global

        /// <summary>
        /// Query if the texture is in a transcodable format.
        /// </summary>
        public bool needsTranscoding => m_Ktx.needsTranscoding;

        /// <summary>
        /// True if the texture has an alpha channel.
        /// </summary>
        public bool hasAlpha => m_Ktx.hasAlpha;

        /// <summary>
        /// True if both pixel width and height are a power of two.
        /// </summary>
        public bool isPowerOfTwo => m_Ktx.isPowerOfTwo;

        /// <summary>
        /// True if both pixel width and height are a multiple of four.
        /// </summary>
        public bool isMultipleOfFour => m_Ktx.isMultipleOfFour;

        /// <summary>
        /// True if texture is square (width equals height)
        /// </summary>
        public bool isSquare => m_Ktx.isSquare;

        /// <summary>
        /// Width of largest mipmap level in pixels
        /// </summary>
        public uint baseWidth => m_Ktx.baseWidth;

        /// <summary>
        /// Height of largest mipmap level in pixels
        /// </summary>
        public uint baseHeight => m_Ktx.baseHeight;

        /// <summary>
        /// Depth of largest mipmap level in pixels
        /// </summary>
        public uint baseDepth => m_Ktx.baseDepth;

        /// <summary>
        /// Number of levels
        /// </summary>
        public uint numLevels => m_Ktx.numLevels;

        /// <summary>
        /// True if texture is of type array
        /// </summary>
        public bool isArray => m_Ktx.isArray;

        /// <summary>
        /// True if texture is of type cube map
        /// </summary>
        public bool isCubemap => m_Ktx.isCubemap;

        /// <summary>
        /// True if texture is compressed
        /// </summary>
        public bool isCompressed => m_Ktx.isCompressed;

        /// <summary>
        /// Number of dimensions
        /// </summary>
        public uint numDimensions => m_Ktx.numDimensions;

        /// <summary>
        /// Number of layers
        /// </summary>
        public uint numLayers => m_Ktx.numLayers;

        /// <summary>
        /// Number of faces (e.g. six for cube maps)
        /// </summary>
        public uint numFaces => m_Ktx.numFaces;

        /// <summary>
        /// Texture's orientation
        /// </summary>
        public TextureOrientation orientation => m_Ktx.orientation;

        // ReSharper restore MemberCanBePrivate.Global

        /// <inheritdoc />
        public override ErrorCode Open(NativeSlice<byte> data)
        {
            KtxNativeInstance.CertifySupportedPlatform();
            return OpenInternal(data);
        }

        /// <inheritdoc />
        public override async Task<TextureResult> LoadTexture2D(
            bool linear = false,
            uint layer = 0,
            uint faceSlice = 0,
            uint mipLevel = 0,
            bool mipChain = true
        )
        {
            Assert.IsNotNull(m_Ktx, "KtxTexture in invalid state. Open has to be called first.");
            return await LoadTexture2DInternal(
                linear,
                layer,
                faceSlice,
                mipLevel,
                mipChain);
        }

        /// <inheritdoc />
        public override async Task<TextureResult> LoadTexture2D(
            GraphicsFormat targetFormat,
            uint layer = 0,
            uint faceSlice = 0,
            uint mipLevel = 0,
            bool mipChain = true
        )
        {
            Assert.IsNotNull(m_Ktx, "KtxTexture in invalid state. Open has to be called first.");
            if (!TranscodeFormatHelper.IsFormatSupported(targetFormat))
            {
                return new TextureResult(ErrorCode.FormatUnsupportedBySystem);
            }
            return await LoadTexture2DInternal(
                true,
                layer,
                faceSlice,
                mipLevel,
                mipChain,
                targetFormat);
        }

        internal async Task<TextureResult> LoadFromBytesInternal(
            NativeSlice<byte> data,
            bool linear = false,
            uint layer = 0,
            uint faceSlice = 0,
            uint mipLevel = 0,
            bool mipChain = true
        )
        {
            var result = new TextureResult
            {
                errorCode = OpenInternal(data)
            };
            if (result.errorCode != ErrorCode.Success) return result;
            result = await LoadTexture2DInternal(linear, layer, faceSlice, mipLevel, mipChain);
            Dispose();
            return result;
        }


        ErrorCode OpenInternal(NativeSlice<byte> data)
        {
            m_Ktx = new KtxNativeInstance();
            return m_Ktx.Load(data);
        }

        async Task<TextureResult> LoadTexture2DInternal(
            bool linear = false,
            uint layer = 0,
            uint faceSlice = 0,
            uint mipLevel = 0,
            bool mipChain = true,
            GraphicsFormat? targetFormat = null
            )
        {
            var result = new TextureResult();
            var graphicsFormat = GraphicsFormat.None;
            if (m_Ktx.valid)
            {
                if (m_Ktx.ktxClass == KtxClassId.KtxTexture2)
                {
                    if (m_Ktx.needsTranscoding)
                    {

                        TranscodeFormatTuple? formats;
                        if (targetFormat.HasValue)
                        {
                            formats = TranscodeFormatHelper.GetTranscodeFormats(targetFormat.Value);
                        }
                        else
                        {
                            // TODO: Maybe do this somewhere more central
                            TranscodeFormatHelper.Init();

                            formats = GetFormat(m_Ktx, m_Ktx, linear);
                        }

                        if (formats.HasValue)
                        {
                            graphicsFormat = formats.Value.format;
#if KTX_VERBOSE
                            Debug.LogFormat(
                                "Transcode to GraphicsFormat {0} ({1})",
                                formats.Value.format,
                                formats.Value.transcodeFormat
                                );
#endif
                            result.errorCode = await TranscodeInternal(
                                m_Ktx,
                                formats.Value.transcodeFormat,
                                layer,
                                faceSlice,
                                mipLevel
                                );
                            result.orientation = m_Ktx.orientation;
                        }
                        else
                        {
                            result.errorCode = ErrorCode.UnsupportedFormat;
                        }
                    }
                    else
                    {
                        graphicsFormat = m_Ktx.graphicsFormat;
                        if (graphicsFormat == GraphicsFormat.None)
                        {
                            result.errorCode = ErrorCode.UnsupportedFormat;
                        }
                        else
                        if (!TranscodeFormatHelper.IsFormatSupported(graphicsFormat, linear))
                        {
                            result.errorCode = ErrorCode.FormatUnsupportedBySystem;
                        }
                    }
                }
                else
                {
                    result.errorCode = ErrorCode.UnsupportedVersion;
                }
            }
            else
            {
                result.errorCode = ErrorCode.LoadingFailed;
            }

            if (result.errorCode != ErrorCode.Success)
            {
                return result;
            }

            Assert.IsTrue(m_Ktx.valid);
            Profiler.BeginSample("CreateTexture");

#if KTX_UNITY_GPU_UPLOAD
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLCore
                || SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES2
                || SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3
               )
            {
                m_Ktx.EnqueueForGpuUpload();

                Texture2D texture;
                bool success;
                while (!m_Ktx.TryCreateTexture(out texture, out success, m_Format)) {
                    Profiler.EndSample();
                    await Task.Yield();
                }

                if (success) {
                    return new TextureResult {
                        texture = texture
                    };
                }
                return new TextureResult(ErrorCode.LoadingFailed);
            }
#endif

            try
            {
                var texture = m_Ktx.LoadTextureData(
                    graphicsFormat,
                    layer,
                    mipLevel,
                    faceSlice,
                    mipChain
                    );
                result.texture = texture;
            }
            catch (UnityException)
            {
                result.errorCode = ErrorCode.LoadingFailed;
            }

            Profiler.EndSample();
            return result;
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            Assert.IsNotNull(m_Ktx, "KtxTexture in invalid state. Open has to be called first.");
            m_Ktx.Unload();
            m_Ktx = null;
        }

        internal GraphicsFormat GetGraphicsFormat()
        {
            if (m_Ktx.valid && m_Ktx.ktxClass == KtxClassId.KtxTexture2 && !m_Ktx.needsTranscoding)
            {
                return m_Ktx.graphicsFormat;
            }

            return GraphicsFormat.None;
        }

        async Task<ErrorCode> TranscodeInternal(
            KtxNativeInstance ktx,
            TranscodeFormat format,
            uint layer,
            uint faceSlice,
            uint mipLevel
            )
        {

            if (layer >= (isArray ? numLayers : 1))
            {
                return ErrorCode.InvalidLayer;
            }

            if (isCubemap && faceSlice >= numFaces)
            {
                return ErrorCode.InvalidFace;
            }

            if (numDimensions > 2 && faceSlice >= baseDepth)
            {
                return ErrorCode.InvalidSlice;
            }

            if (mipLevel >= numLevels)
            {
                return ErrorCode.InvalidLevel;
            }

            var result = ErrorCode.Success;

            Profiler.BeginSample("KtxTranscode");

            var job = new KtxTranscodeJob();

            var jobHandle = ktx.LoadBytesJob(ref job, format);

            Profiler.EndSample();

            while (!jobHandle.IsCompleted)
            {
                await Task.Yield();
            }
            jobHandle.Complete();

            if (job.result[0] != KtxErrorCode.Success)
            {
                result = ErrorCode.TranscodeFailed;
            }
            job.result.Dispose();

            return result;
        }
    }
}
