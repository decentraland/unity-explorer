// SPDX-FileCopyrightText: 2023 Unity Technologies and the KTX for Unity authors
// SPDX-License-Identifier: Apache-2.0


using System.Runtime.InteropServices;
using Unity.Jobs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using IntPtr = System.IntPtr;

namespace KtxUnity
{
    unsafe struct BasisUniversalJob : IJob
    {
        [WriteOnly]
        public NativeArray<bool> result;

        [ReadOnly]
        public TranscodeFormat format;

        [ReadOnly]
        public uint mipLevel;

        [ReadOnly]
        [NativeDisableUnsafePtrRestriction]
        public IntPtr nativeReference;

        [ReadOnly]
        public NativeArray<uint> sizes;

        [ReadOnly]
        public NativeArray<uint> offsets;

        [ReadOnly]
        public uint layer;

        [WriteOnly]
        public NativeArray<byte> textureData;

        public void Execute()
        {
            var success = ktx_basisu_startTranscoding(nativeReference);
            var textureDataPtr = textureData.GetUnsafePtr();
            for (uint i = 0; i < offsets.Length; i++)
            {
                success = success &&
                ktx_basisu_transcodeImage(
                    nativeReference,
                    (byte*)textureDataPtr + offsets[(int)i],
                    sizes[(int)i],
                    layer,
                    mipLevel + i,
                    (uint)format,
                    0,
                    0
                    );
                if (!success) break;
            }
            result[0] = success;
        }

        [DllImport(KtxNativeInstance.ktxLibrary)]
        static extern bool ktx_basisu_startTranscoding(IntPtr basis);

        [DllImport(KtxNativeInstance.ktxLibrary)]
        static extern bool ktx_basisu_transcodeImage(
            IntPtr basis,
            void* dst,
            uint dstSize,
            uint imageIndex,
            uint levelIndex,
            uint format,
            uint pvrtcWrapAddressing,
            uint getAlphaForOpaqueFormats
            );
    }

    struct KtxTranscodeJob : IJob
    {

        [WriteOnly]
        public NativeArray<KtxErrorCode> result;

        [ReadOnly]
        [NativeDisableUnsafePtrRestriction]
        public IntPtr nativeReference;

        [ReadOnly]
        public TranscodeFormat outputFormat;

        public void Execute()
        {
            result[0] = KtxNativeInstance.ktxTexture2_TranscodeBasis(
                nativeReference,
                outputFormat,
                0 // transcodeFlags
                );
        }
    }
}
