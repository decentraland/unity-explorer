// SPDX-FileCopyrightText: 2023 Unity Technologies and the KTX for Unity authors
// SPDX-License-Identifier: Apache-2.0



using System.Runtime.InteropServices;
using System;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Profiling;


namespace KtxUnity
{

    // Source: basisu_transcoder.h -> basist::basis_texture_type
    enum BasisUniversalTextureType
    {
        /// <summary>
        /// An arbitrary array of 2D RGB or RGBA images with optional mipmaps,
        /// array size = # images, each image may have a different resolution
        /// and # of mipmap levels
        /// </summary>
        Image2D = 0,

        /// <summary>
        /// An array of 2D RGB or RGBA images with optional mipmaps,
        /// array size = # images, each image has the same resolution and
        /// mipmap levels
        /// </summary>
        Image2DArray = 1,

        /// <summary>
        /// An array of cubemap levels,
        /// total # of images must be dividable by 6,
        /// in X+, X-, Y+, Y-, Z+, Z- order, with optional mipmaps
        /// </summary>
        CubemapArray = 2,

        /// <summary>
        /// An array of 2D video frames, with optional mipmaps,
        /// # frames = # images, each image has the same resolution and # of
        /// mipmap levels
        /// </summary>
        VideoFrames = 3,

        /// <summary>
        /// A 3D texture with optional mipmaps, Z dimension = # images,
        /// each image has the same resolution and # of mipmap levels
        /// </summary>
        Volume = 4,
    }

    class BasisUniversalTranscoderInstance
    {
        public IntPtr nativeReference;

        public BasisUniversalTranscoderInstance(IntPtr nativeReference)
        {
            this.nativeReference = nativeReference;
        }

        public unsafe bool Open(NativeSlice<byte> data)
        {
            void* src = data.GetUnsafeReadOnlyPtr();
            bool success = ktx_basisu_open_basis(nativeReference, src, data.Length);
#if DEBUG
            if(!success) {
                Debug.LogError("Couldn't validate BasisU header!");
            }
#endif
            return success;
        }

        public MetaData LoadMetaData()
        {
            Profiler.BeginSample("LoadMetaData");
            MetaData meta = new MetaData();
            meta.hasAlpha = GetHasAlpha();
            var imageCount = GetImageCount();
            meta.images = new ImageInfo[imageCount];
            for (uint i = 0; i < imageCount; i++)
            {
                var ii = new ImageInfo();
                var levelCount = GetLevelCount(i);
                ii.levels = new LevelInfo[levelCount];
                for (uint l = 0; l < levelCount; l++)
                {
                    var li = new LevelInfo();
                    GetImageSize(out li.width, out li.height, i, l);
                    ii.levels[l] = li;
                }
                meta.images[i] = ii;
            }
            Profiler.EndSample();
            return meta;
        }

        public void Close()
        {
            ktx_basisu_close_basis(nativeReference);
        }

        public bool GetHasAlpha()
        {
            return ktx_basisu_getHasAlpha(nativeReference);
        }

        public uint GetImageCount()
        {
            return ktx_basisu_getNumImages(nativeReference);
        }

        public uint GetLevelCount(uint imageIndex)
        {
            return ktx_basisu_getNumLevels(nativeReference, imageIndex);
        }

        public void GetImageSize(out uint width, out uint height, uint imageIndex = 0, uint levelIndex = 0)
        {
            width = ktx_basisu_getImageWidth(nativeReference, imageIndex, levelIndex);
            height = ktx_basisu_getImageHeight(nativeReference, imageIndex, levelIndex);
        }

        public bool GetYFlip()
        {
            return !ktx_basisu_get_y_flip(nativeReference);
        }

        // public bool GetIsEtc1s() {
        //     return ktx_basisu_get_is_etc1s(nativeReference);
        // }

        public BasisUniversalTextureType GetTextureType()
        {
            return ktx_basisu_get_texture_type(nativeReference);
        }

        public uint GetImageTranscodedSize(uint imageIndex, uint levelIndex, TranscodeFormat format)
        {
            return ktx_basisu_getImageTranscodedSizeInBytes(nativeReference, imageIndex, levelIndex, (uint)format);
        }

        public unsafe bool Transcode(uint imageIndex, uint levelIndex, TranscodeFormat format, out byte[] transcodedData)
        {
            Profiler.BeginSample("BasisU.Transcode");
            transcodedData = null;

            if (!ktx_basisu_startTranscoding(nativeReference))
            {
                Profiler.EndSample();
                return false;
            }

            var size = GetImageTranscodedSize(imageIndex, levelIndex, format);
            var data = new byte[size];

            bool result;
            fixed (void* dst = &(data[0]))
            {
                result = ktx_basisu_transcodeImage(nativeReference, dst, size, imageIndex, levelIndex, (uint)format, 0, 0);
            }
            transcodedData = data;
            Profiler.EndSample();
            return result;
        }

        ~BasisUniversalTranscoderInstance()
        {
            ktx_basisu_delete_basis(nativeReference);
        }

        [DllImport(KtxNativeInstance.ktxLibrary)]
        static extern unsafe bool ktx_basisu_open_basis(IntPtr basis, void* data, int length);

        [DllImport(KtxNativeInstance.ktxLibrary)]
        static extern void ktx_basisu_close_basis(IntPtr basis);

        [DllImport(KtxNativeInstance.ktxLibrary)]
        static extern void ktx_basisu_delete_basis(IntPtr basis);

        [DllImport(KtxNativeInstance.ktxLibrary)]
        static extern bool ktx_basisu_getHasAlpha(IntPtr basis);

        [DllImport(KtxNativeInstance.ktxLibrary)]
        static extern uint ktx_basisu_getNumImages(IntPtr basis);

        [DllImport(KtxNativeInstance.ktxLibrary)]
        static extern uint ktx_basisu_getNumLevels(IntPtr basis, uint imageIndex);

        [DllImport(KtxNativeInstance.ktxLibrary)]
        static extern uint ktx_basisu_getImageWidth(IntPtr basis, uint imageIndex, uint levelIndex);

        [DllImport(KtxNativeInstance.ktxLibrary)]
        static extern uint ktx_basisu_getImageHeight(IntPtr basis, uint imageIndex, uint levelIndex);

        [DllImport(KtxNativeInstance.ktxLibrary)]
        static extern bool ktx_basisu_get_y_flip(IntPtr basis);

        // [DllImport(KtxNativeInstance.ktxLibrary)]
        // private static extern bool ktx_basisu_get_is_etc1s( IntPtr basis );

        [DllImport(KtxNativeInstance.ktxLibrary)]
        static extern BasisUniversalTextureType ktx_basisu_get_texture_type(IntPtr basis);

        [DllImport(KtxNativeInstance.ktxLibrary)]
        static extern uint ktx_basisu_getImageTranscodedSizeInBytes(IntPtr basis, uint imageIndex, uint levelIndex, uint format);

        [DllImport(KtxNativeInstance.ktxLibrary)]
        static extern bool ktx_basisu_startTranscoding(IntPtr basis);

        [DllImport(KtxNativeInstance.ktxLibrary)]
        static extern unsafe bool ktx_basisu_transcodeImage(IntPtr basis, void* dst, uint dstSize, uint imageIndex, uint levelIndex, uint format, uint pvrtcWrapAddressing, uint getAlphaForOpaqueFormats);
    }
}
