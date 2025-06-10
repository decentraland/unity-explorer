// SPDX-FileCopyrightText: 2023 Unity Technologies and the KTX for Unity authors
// SPDX-License-Identifier: Apache-2.0


using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Profiling;
using Unity.Collections;

namespace KtxUnity
{
    /// <summary>
    /// Loads a Basis Universal texture from the StreamingAssets folder, a URL, or a buffer.
    /// </summary>
    public class BasisUniversalTexture : TextureBase
    {

        NativeSlice<byte> m_InputData;
        NativeArray<byte> m_TextureData;
        MetaData m_MetaData;
        TextureOrientation m_Orientation;

        /// <inheritdoc />
        public override ErrorCode Open(NativeSlice<byte> data)
        {
            KtxNativeInstance.CertifySupportedPlatform();
            m_InputData = data;
            return ErrorCode.Success;
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
            KtxNativeInstance.CertifySupportedPlatform();
            return await LoadTexture2DInternal(
                linear,
                layer,
                0,
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
            KtxNativeInstance.CertifySupportedPlatform();
            return await LoadTexture2DInternal(
                true,
                layer,
                0,
                mipLevel,
                mipChain,
                targetFormat);
        }

        /// <inheritdoc />
        public override void Dispose() { }

        internal async Task<TextureResult> LoadFromBytesInternal(
            NativeSlice<byte> data,
            bool linear = false,
            uint layer = 0,
            uint faceSlice = 0,
            uint mipLevel = 0,
            bool mipChain = true
        )
        {
            m_InputData = data;
            var result = await LoadTexture2DInternal(linear, layer, faceSlice, mipLevel, mipChain);
            Dispose();
            return result;
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
            var transcoder = BasisUniversal.GetTranscoderInstance();

            while (transcoder == null)
            {
                await Task.Yield();
                transcoder = BasisUniversal.GetTranscoderInstance();
            }

            var result = new TextureResult();

            GraphicsFormat format;

            if (transcoder.Open(m_InputData))
            {
                m_MetaData = transcoder.LoadMetaData();

                var formatTuple = targetFormat.HasValue
                    ? TranscodeFormatHelper.GetTranscodeFormats(targetFormat.Value)
                    : GetFormat(m_MetaData, m_MetaData.images[layer].levels[0], linear);

                if (formatTuple.HasValue)
                {
                    var formats = formatTuple.Value;

#if KTX_VERBOSE
                    Debug.LogFormat("LoadTexture2D to GraphicsFormat {0} ({1})",formats.format,formats.transcodeFormat);
#endif

                    format = formats.format;
                    result.errorCode = await Transcode(
                        transcoder,
                        formats.transcodeFormat,
                        layer,
                        mipLevel,
                        mipChain
                    );
                }
                else
                {
                    BasisUniversal.ReturnTranscoderInstance(transcoder);
                    result.errorCode = ErrorCode.UnsupportedFormat;
                    return result;
                }

                m_Orientation = TextureOrientation.KtxDefault;
                if (!transcoder.GetYFlip())
                {
                    // Regular basis files (no y_flip) seem to be
                    m_Orientation |= TextureOrientation.YUp;
                }
                BasisUniversal.ReturnTranscoderInstance(transcoder);
            }
            else
            {
                BasisUniversal.ReturnTranscoderInstance(transcoder);
                result.errorCode = ErrorCode.LoadingFailed;
                return result;
            }

            Profiler.BeginSample("LoadBytesRoutineGpuUpload");

            m_MetaData.GetSize(out var width, out var height, layer, mipLevel);
            var flags = KtxNativeInstance.defaultTextureCreationFlags;
            if (mipChain && m_MetaData.images[layer].levels.Length - mipLevel > 1)
            {
                flags |= TextureCreationFlags.MipChain;
            }

            result.texture = new Texture2D((int)width, (int)height, format, flags);
            result.orientation = m_Orientation;

#if KTX_UNITY_GPU_UPLOAD
            // TODO: native GPU upload
#else
#endif

            result.texture.LoadRawTextureData(m_TextureData);
            result.texture.Apply(false, true);
            m_TextureData.Dispose();
            Profiler.EndSample();
            return result;
        }

        async Task<ErrorCode> Transcode(
            BasisUniversalTranscoderInstance transcoder,
            TranscodeFormat transcodeFormat,
            uint layer,
            uint mipLevel,
            bool mipChain
            )
        {
            var result = ErrorCode.Success;

            Profiler.BeginSample("BasisUniversalJob");
            var job = new BasisUniversalJob
            {
                layer = layer,
                mipLevel = mipLevel,
                result = new NativeArray<bool>(1, KtxNativeInstance.defaultAllocator)
            };

            var jobHandle = BasisUniversal.LoadBytesJob(
                ref job,
                transcoder,
                transcodeFormat,
                mipChain
                );

            m_TextureData = job.textureData;
            Profiler.EndSample();

            while (!jobHandle.IsCompleted)
            {
                await Task.Yield();
            }
            jobHandle.Complete();

            if (!job.result[0])
            {
                m_TextureData.Dispose();
                result = ErrorCode.TranscodeFailed;
            }
            job.sizes.Dispose();
            job.offsets.Dispose();
            job.result.Dispose();

            return result;
        }
    }
}
