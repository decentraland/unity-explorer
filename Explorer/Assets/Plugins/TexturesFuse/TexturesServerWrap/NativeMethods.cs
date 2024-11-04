using DCL.Diagnostics;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Plugins.TexturesFuse.TexturesServerWrap
{
    [SuppressMessage("ReSharper", "EnumUnderlyingTypeIsInt")]
    public static class NativeMethods
    {
        private const string LIBRARY_NAME = "libtexturesfuse";
        private const string PREFIX = "texturesfuse_";

        private const int AMD_MAX_CMDS = 20;
        private const int AMD_MAX_CMD_STR = 32;
        private const int AMD_MAX_CMD_PARAM = 16;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void OutputMessageDelegate(int format, string message);

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [SuppressMessage("ReSharper", "ArrangeTypeMemberModifiers")]
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct InitOptions
        {
#pragma region ASTC_options

            public int ASTCProfile;

            private uint blockX;
            private uint blockY;

            [Header("X and Y should be installed via unity texture format")]
            public uint blockZ;
            public float quality;
            public uint flags;
#pragma endregion ASTC_options

            /// <summary>
            ///     Could be null
            /// </summary>
            public OutputMessageDelegate? outputMessage;

            public InitOptions NewWithMode(Mode mode)
            {
                const int MinimalSupportedBlock = 4;

                var output = this;
                var sizeResult = mode.ASTCChunkSize();

                if (sizeResult.Success == false)
                {
                    ReportHub.LogWarning(
                        ReportCategory.TEXTURES,
                        $"Error during ASTC chunk initialization {sizeResult.ErrorMessage}"
                    );

                    output.blockX = MinimalSupportedBlock;
                    output.blockY = MinimalSupportedBlock;
                }
                else
                {
                    output.blockX = sizeResult.Value;
                    output.blockY = sizeResult.Value;
                }

                return output;
            }

            public static InitOptions NewDefault() =>
                new ()
                {
                    ASTCProfile = 0,
                    blockX = 4,
                    blockY = 4,
                    blockZ = 1,
                    quality = 60f,
                    flags = 0,
                };
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [SuppressMessage("ReSharper", "ArrangeTypeMemberModifiers")]
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct Swizzle
        {
            public int r;
            public int g;
            public int b;
            public int a;

            public static Swizzle NewDefault() =>
                new ()
                {
                    r = 0,
                    g = 1,
                    b = 2,
                    a = 3,
                };
        }

        public enum ImageResult : int
        {
            ErrorNotImplemented = -1,
            ErrorUnknown = 0,
            Success = 1,
            ErrorOpenMemoryStream = 2,
            ErrorUnknownImageFormat = 3,
            ErrorCannotLoadImage = 4,
            ErrorCannotGetBits = 5,
            ErrorCannotDownscale = 6,
            ErroConvertImageToAlphaUnsupportedInputFormat = 7,
            ErrorOnConvertImageToAlpha = 8,
            ErrorWrongAlphaImage = 9,

            ErrorInvalidPointer = 10,
            ErrorASTCOnInit = 11,
            ErrorASTCOnAlloc = 12,
            ErrorASTCOnCompress = 13,

            ErrorDisposeAlreadyDisposed = 20,
            ErrorDisposeNotAllTexturesReleased = 21,

            ErrorReleaseNoHandleFound = 30,

            ErrorASTC_OUT_OF_MEM = 40,
            ErrorASTC_BAD_CPU_FLOAT = 41,
            ErrorASTC_BAD_PARAM = 42,
            ErrorASTC_BAD_BLOCK_SIZE = 43,
            ErrorASTC_BAD_PROFILE = 44,
            ErrorASTC_BAD_QUALITY = 45,
            ErrorASTC_BAD_SWIZZLE = 46,
            ErrorASTC_BAD_FLAGS = 47,
            ErrorASTC_BAD_CONTEXT = 48,
            ErrorASTC_NOT_IMPLEMENTED = 49,
            ErrorASTC_BAD_DECODE_MODE = 50,
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [SuppressMessage("ReSharper", "ArrangeTypeMemberModifiers")]
        public enum CMP_GPUDecode
        {
            GPUDecode_OPENGL = 0, // Use OpenGL   to decode Textures (default)
            GPUDecode_DIRECTX, // Use DirectX  to decode Textures
            GPUDecode_VULKAN, // Use Vulkan  to decode Textures
            GPUDecode_INVALID
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [SuppressMessage("ReSharper", "ArrangeTypeMemberModifiers")]
        public enum CMP_Compute_type
        {
            CMP_UNKNOWN = 0,
            CMP_CPU = 1, //Use CPU Only, encoders defined CMP_CPUEncode or Compressonator lib will be used
            CMP_HPC = 2, //Use CPU High Performance Compute Encoders with SPMD support defined in CMP_CPUEncode)
            CMP_GPU_OCL = 3, //Use GPU Kernel Encoders to compress textures using OpenCL Framework
            CMP_GPU_DXC = 4, //Use GPU Kernel Encoders to compress textures using DirectX Compute Framework
            CMP_GPU_VLK = 5, //Use GPU Kernel Encoders to compress textures using Vulkan Compute Framework
            CMP_GPU_HW = 6 //Use GPU HW to encode textures , using gl extensions
        }

        // An enum selecting the speed vs. quality trade-off.
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [SuppressMessage("ReSharper", "ArrangeTypeMemberModifiers")]
        public enum CMP_Speed
        {
            CMP_Speed_Normal, // Highest quality mode
            CMP_Speed_Fast, // Slightly lower quality but much faster compression mode - DXTn & ATInN only
            CMP_Speed_SuperFast, // Slightly lower quality but much, much faster compression mode - DXTn & ATInN only
        };

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [SuppressMessage("ReSharper", "ArrangeTypeMemberModifiers")]
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct KernelDeviceInfo
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            byte[] m_deviceName; //[256];  // Device name (CPU or GPU)
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
            byte[] m_version; //[128];     // Kernel pipeline version number (CPU or GPU)
            int m_maxUCores; // Max Unit device CPU cores or GPU compute units (CU)

            // AMD GCN::One compute unit combines 64 shader processors
            // with 4 Texture Mapping units (TMU)
        };

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [SuppressMessage("ReSharper", "ArrangeTypeMemberModifiers")]
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct KernelPerformanceStats
        {
            float m_computeShaderElapsedMS; // Total Elapsed Shader Time to process all the blocks
            int m_num_blocks; // Number of Texel (Typically 4x4) blocks
            float m_CmpMTxPerSec; // Number of Mega Texels processed per second
        };

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [SuppressMessage("ReSharper", "ArrangeTypeMemberModifiers")]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void CMP_PrintInfoStr([MarshalAs(UnmanagedType.LPStr)] string infoStr);

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [SuppressMessage("ReSharper", "ArrangeTypeMemberModifiers")]
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct AMD_CMD_SET
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = AMD_MAX_CMD_STR)]
            byte[] strCommand; //[AMD_MAX_CMD_STR];

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = AMD_MAX_CMD_PARAM)]
            byte[] strParameter; //[AMD_MAX_CMD_PARAM];
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [SuppressMessage("ReSharper", "ArrangeTypeMemberModifiers")]
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct CMP_CompressOptions
        {
            public uint dwSize; // The size of this structure.

            // New to v4.5
            // Flags to control parameters in Brotli-G compression preconditioning
            public bool doPreconditionBRLG;
            public bool doDeltaEncodeBRLG;
            public bool doSwizzleBRLG;

            // New to v4.3
            public uint dwPageSize; // Used by Brotli-G Codec for setting the page size used for compression

            // New to v4.2
            public bool bUseRefinementSteps; // Used by BC1, BC2, and BC3 codecs to improve quality,

            // this setting will increase encoding time for better quality results
            public int nRefinementSteps; // Currently only 1 step is implemented

            // v4.1 and older settings
            public bool bUseChannelWeighting; // Use channel weights. With swizzled formats the weighting applies to the data within the specified

            // channel not the channel itself. Channel weigthing is not implemented for BC6H and BC7
            public float fWeightingRed; // The weighting of the Red or X Channel.
            public float fWeightingGreen; // The weighting of the Green or Y Channel.
            public float fWeightingBlue; // The weighting of the Blue or Z Channel.
            public bool bUseAdaptiveWeighting; // Adapt weighting on a per-block basis.
            public bool bDXT1UseAlpha; // Encode single-bit alpha data. Only valid when compressing to DXT1 & BC1.
            public bool bUseGPUDecompress; // Use GPU to decompress. Decode API can be changed by specified in DecodeWith parameter. Default is OpenGL.
            public bool bUseCGCompress; // Use SPMD/GPU to compress. Encode API can be changed by specified in EncodeWith parameter. Default is OpenCL.
            public byte nAlphaThreshold; // The alpha threshold to use when compressing to DXT1 & BC1 with bDXT1UseAlpha.

            // Texels with an alpha value less than the threshold are treated as transparent.
            // Note: When nCompressionSpeed is not set to Normal AphaThreshold is ignored for DXT1 & BC1
            public bool bDisableMultiThreading; // Disable multi-threading of the compression. This will slow the compression but can be

            // useful if you're managing threads in your application.
            // if set BC7 dwnumThreads will default to 1 during encoding and then return back to its original value when done.
            public CMP_Speed nCompressionSpeed; // The trade-off between compression speed & quality.

            // Notes:
            // 1. This value is ignored for BC6H and BC7 (for BC7 the compression speed depends on fquaility value)
            // 2. For 64 bit DXT1 to DXT5 and BC1 to BC5 nCompressionSpeed is ignored and set to Noramal Speed
            // 3. To force the use of nCompressionSpeed setting regarless of Note 2 use fQuality at 0.05
            public CMP_GPUDecode nGPUDecode; // This value is set using DecodeWith argument (OpenGL, DirectX) default is OpenGL
            public CMP_Compute_type nEncodeWith; // This value is set using EncodeWith argument, currently only OpenCL is used
            public uint dwnumThreads; // Number of threads to initialize for BC7 encoding (Max up to 128). Default set to auto,
            public float fquality; // Quality of encoding. This value ranges between 0.0 and 1.0. BC7 & BC6 default is 0.05, others codecs are set at 1.0

            // setting fquality above 0.0 gives the fastest, lowest quality encoding, 1.0 is the slowest,
            // highest quality encoding. Default set to a low value of 0.05
            public bool brestrictColour; // This setting is a quality tuning setting for BC7 which may be necessary for convenience in some

            // applications. Default set to false. If set and the block does not need alpha it instructs
            //  the code not to use modes that have combined colour + alpha - this avoids the possibility that the encoder might
            //  choose an alpha other than 1.0 (due to parity) and cause something to become accidentally slightly transparent
            //  (it's possible that when encoding 3-component texture applications will assume that the 4th component can
            //  safely be assumed to be 1.0 all the time.)
            public bool brestrictAlpha; // This setting is a quality tuning setting for BC7 which may be necessary for some textures. Default set to false,

            // if set it will also apply restriction to blocks with alpha to avoid issues with punch-through
            // or thresholded alpha encoding
            public uint dwmodeMask; // Mode to set BC7 to encode blocks using any of 8 different block modes in order to obtain the highest quality. Default set to 0xFF)

            // You can combine the bits to test for which modes produce the best image quality.
            // The mode that produces the best image quality above a set quality level (fquality) is used and subsequent modes set in the mask
            // are not tested, this optimizes the performance of the compression versus the required quality.
            // If you prefer to check all modes regardless of the quality then set the fquality to a value of 0
            public int NumCmds; // Count of the number of command value pairs in CmdSet[].  Max value that can be set is AMD_MAX_CMDS = 20 on this release

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = AMD_MAX_CMDS)]
            public AMD_CMD_SET[] CmdSet; // Extended command options that can be set for the specified codec\n

            // Example to set the number of threads and quality used for compression\n
            //        CMP_CompressOptions Options;\n
            //        memset(Options,0,sizeof(CMP_CompressOptions));\n
            //        Options.dwSize = sizeof(CMP_CompressOptions)\n
            //        Options.CmdSet[0].strCommand   = "NumThreads"\n
            //        Options.CmdSet[0].strParameter = "8";\n
            //        Options.CmdSet[1].strCommand   = "Quality"\n
            //        Options.CmdSet[1].strParameter = "1.0";\n
            //        Options.NumCmds = 2;\n
            public float fInputDefog; // ToneMap properties for float type image send into non float compress algorithm.
            public float fInputExposure; //
            public float fInputKneeLow; //
            public float fInputKneeHigh; //
            public float fInputGamma; //
            public float fInputFilterGamma; // Gamma correction value applied for mipmap generation

            public int iCmpLevel; // < draco setting: compression level (range 0-10: higher mean more compressed) - default 7
            public int iPosBits; // quantization bits for position - default 14
            public int iTexCBits; // quantization bits for texture coordinates - default 12
            public int iNormalBits; // quantization bits for normal - default 10
            public int iGenericBits; // quantization bits for generic - default 8

#if USE_3DMESH_OPTIMIZE
            int iVcacheSize; // For mesh vertices optimization, hardware vertex cache size. (value range 1 - no limit as it

            // allows users to simulate hardware cache size to find the most optimum size)- default is enabled with cache size = 16
            int iVcacheFIFOSize; // For mesh vertices optimization, hardware vertex cache size. (value range 1 - no limit as it

            // allows users to simulate hardware cache size to find the most optimum size)- default is disabled.
            float fOverdrawACMR; // For mesh overdraw optimization,  optimize overdraw with ACMR (average cache miss ratio)

            // threshold value specified (value range 1-3) - default is enabled with ACMR value = 1.05 (i.e. 5% worse)
            int iSimplifyLOD; // simplify mesh using LOD (Level of Details) value specified.(value range 1- no limit as it allows users

            // to simplify the mesh until the level they desired. Higher level means less triangles drawn, less details.)
            bool bVertexFetch; // optimize vertices fetch . boolean value 0 - disabled, 1-enabled. -default is enabled.
#endif

            public CMP_FORMAT SourceFormat;
            public CMP_FORMAT DestFormat;
            public bool format_support_hostEncoder; // Temp setting used while encoding with gpu or hpc plugins

            // User Print Info interface
            public CMP_PrintInfoStr m_PrintInfoStr;

            // User Info for Performance Query on GPU or CPU Encoder Processing
            public bool getPerfStats; // Set to true if you want to get Performance Stats
            public KernelPerformanceStats perfStats; // Data storage for the performance stats obtained from GPU or CPU while running encoder processing
            public bool getDeviceInfo; // Set to true if you want to get target device info
            public KernelDeviceInfo deviceInfo; // Data storage for the performance stats obtained from GPU or CPU while running encoder processing
            public bool genGPUMipMaps; // When ecoding with GPU HW use it to generate MipMap images, valid only when miplevels is set else default is toplevel 1
            public bool useSRGBFrames; // when using GPU HW for encoding and mipmap generation use SRGB frames, default is RGB
            public int miplevels; // miplevels to use when GPU is used to generate them

            public static CMP_CompressOptions NewDefault()
            {
                return new CMP_CompressOptions
                {
                    dwSize = 0,// Defined on native side
                    dwnumThreads = 1,
                    fquality = 1,//highest quality
                    bDisableMultiThreading = true,
                    nCompressionSpeed = CMP_Speed.CMP_Speed_Normal,
                };
            }
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [SuppressMessage("ReSharper", "ArrangeTypeMemberModifiers")]
        public enum CMP_FORMAT : int
        {
            CMP_FORMAT_Unknown = 0x0000, // Undefined texture format.

            // Key to format types 0xFnbC
            // C = 0 is uncompressed C > 0 is compressed
            //
            // For C = 0 uncompressed
            // F = 1 is Float data, F = 0 is Byte data,
            // nb is a format type
            //
            // For C >= 1 Compressed
            // F = 1 is signed data, F = 0 is unsigned data,
            // b = format is a BCn block comprerssor where b is 1..7 for BC1..BC7,
            // C > 1 is a varaiant of the format (example: swizzled format for DXTC, or a signed version)
            //

            // Channel Component formats --------------------------------------------------------------------------------
            // Byte Format 0x0nn0
            CMP_FORMAT_RGBA_8888_S = 0x0010, // RGBA format with signed 8-bit fixed channels.
            CMP_FORMAT_ARGB_8888_S = 0x0020, // ARGB format with signed 8-bit fixed channels.
            CMP_FORMAT_ARGB_8888 = 0x0030, // ARGB format with 8-bit fixed channels.
            CMP_FORMAT_ABGR_8888 = 0x0040, // ABGR format with 8-bit fixed channels.
            CMP_FORMAT_RGBA_8888 = 0x0050, // RGBA format with 8-bit fixed channels.
            CMP_FORMAT_BGRA_8888 = 0x0060, // BGRA format with 8-bit fixed channels.
            CMP_FORMAT_RGB_888 = 0x0070, // RGB format with 8-bit fixed channels.
            CMP_FORMAT_RGB_888_S = 0x0080, // RGB format with 8-bit fixed channels.
            CMP_FORMAT_BGR_888 = 0x0090, // BGR format with 8-bit fixed channels.
            CMP_FORMAT_RG_8_S = 0x00A0, // Two component format with signed 8-bit fixed channels.
            CMP_FORMAT_RG_8 = 0x00B0, // Two component format with 8-bit fixed channels.
            CMP_FORMAT_R_8_S = 0x00C0, // Single component format with signed 8-bit fixed channel.
            CMP_FORMAT_R_8 = 0x00D0, // Single component format with 8-bit fixed channel.
            CMP_FORMAT_ARGB_2101010 = 0x00E0, // ARGB format with 10-bit fixed channels for color & a 2-bit fixed channel for alpha.
            CMP_FORMAT_RGBA_1010102 = 0x00F0, // RGBA format with 10-bit fixed channels for color & a 2-bit fixed channel for alpha.
            CMP_FORMAT_ARGB_16 = 0x0100, // ARGB format with 16-bit fixed channels.
            CMP_FORMAT_ABGR_16 = 0x0110, // ABGR format with 16-bit fixed channels.
            CMP_FORMAT_RGBA_16 = 0x0120, // RGBA format with 16-bit fixed channels.
            CMP_FORMAT_BGRA_16 = 0x0130, // BGRA format with 16-bit fixed channels.
            CMP_FORMAT_RG_16 = 0x0140, // Two component format with 16-bit fixed channels.
            CMP_FORMAT_R_16 = 0x0150, // Single component format with 16-bit fixed channels.

            // Float Format 0x1nn0
            CMP_FORMAT_RGBE_32F = 0x1000, // RGB format with 9-bit floating point each channel and shared 5 bit exponent
            CMP_FORMAT_ARGB_16F = 0x1010, // ARGB format with 16-bit floating-point channels.
            CMP_FORMAT_ABGR_16F = 0x1020, // ABGR format with 16-bit floating-point channels.
            CMP_FORMAT_RGBA_16F = 0x1030, // RGBA format with 16-bit floating-point channels.
            CMP_FORMAT_BGRA_16F = 0x1040, // BGRA format with 16-bit floating-point channels.
            CMP_FORMAT_RG_16F = 0x1050, // Two component format with 16-bit floating-point channels.
            CMP_FORMAT_R_16F = 0x1060, // Single component with 16-bit floating-point channels.
            CMP_FORMAT_ARGB_32F = 0x1070, // ARGB format with 32-bit floating-point channels.
            CMP_FORMAT_ABGR_32F = 0x1080, // ABGR format with 32-bit floating-point channels.
            CMP_FORMAT_RGBA_32F = 0x1090, // RGBA format with 32-bit floating-point channels.
            CMP_FORMAT_BGRA_32F = 0x10A0, // BGRA format with 32-bit floating-point channels.
            CMP_FORMAT_RGB_32F = 0x10B0, // RGB format with 32-bit floating-point channels.
            CMP_FORMAT_BGR_32F = 0x10C0, // BGR format with 32-bit floating-point channels.
            CMP_FORMAT_RG_32F = 0x10D0, // Two component format with 32-bit floating-point channels.
            CMP_FORMAT_R_32F = 0x10E0, // Single component with 32-bit floating-point channels.

            // Lossless Based Compression Formats --------------------------------------------------------------------------------
            // Format 0x2nn0
            CMP_FORMAT_BROTLIG = 0x2000, //< Lossless CMP format compression : Prototyping

            // Compression formats ------------ GPU Mapping DirectX, Vulkan and OpenGL formats and comments --------
            // Compressed Format 0xSnn1..0xSnnF   (Keys 0x00Bv..0x00Bv) S =1 is signed, 0 = unsigned, B =Block Compressors 1..7 (BC1..BC7) and v > 1 is a variant like signed or swizzle
            CMP_FORMAT_BC1 = 0x0011, // DXGI_FORMAT_BC1_UNORM GL_COMPRESSED_RGBA_S3TC_DXT1_EXT A four component opaque (or 1-bit alpha)

            // compressed texture format for Microsoft DirectX10. Identical to DXT1.  Four bits per pixel.
            CMP_FORMAT_BC2 = 0x0021, // DXGI_FORMAT_BC2_UNORM VK_FORMAT_BC2_UNORM_BLOCK GL_COMPRESSED_RGBA_S3TC_DXT3_EXT A four component

            // compressed texture format with explicit alpha for Microsoft DirectX10. Identical to DXT3. Eight bits per pixel.
            CMP_FORMAT_BC3 = 0x0031, // DXGI_FORMAT_BC3_UNORM VK_FORMAT_BC3_UNORM_BLOCK GL_COMPRESSED_RGBA_S3TC_DXT5_EXT A four component

            // compressed texture format with interpolated alpha for Microsoft DirectX10. Identical to DXT5. Eight bits per pixel.
            CMP_FORMAT_BC4 = 0x0041, // DXGI_FORMAT_BC4_UNORM VK_FORMAT_BC4_UNORM_BLOCK GL_COMPRESSED_RED_RGTC1 A single component

            // compressed texture format for Microsoft DirectX10. Identical to ATI1N. Four bits per pixel.
            CMP_FORMAT_BC4_S = 0x1041, // DXGI_FORMAT_BC4_SNORM VK_FORMAT_BC4_SNORM_BLOCK GL_COMPRESSED_SIGNED_RED_RGTC1 A single component

            // compressed texture format for Microsoft DirectX10. Identical to ATI1N. Four bits per pixel.
            CMP_FORMAT_BC5 = 0x0051, // DXGI_FORMAT_BC5_UNORM VK_FORMAT_BC5_UNORM_BLOCK GL_COMPRESSED_RG_RGTC2 A two component

            // compressed texture format for Microsoft DirectX10. Identical to ATI2N_XY. Eight bits per pixel.
            CMP_FORMAT_BC5_S = 0x1051, // DXGI_FORMAT_BC5_SNORM VK_FORMAT_BC5_SNORM_BLOCK GL_COMPRESSED_RGBA_BPTC_UNORM A two component

            // compressed texture format for Microsoft DirectX10. Identical to ATI2N_XY. Eight bits per pixel.
            CMP_FORMAT_BC6H = 0x0061, // DXGI_FORMAT_BC6H_UF16 VK_FORMAT_BC6H_UFLOAT_BLOCK GL_COMPRESSED_RGB_BPTC_UNSIGNED_FLOAT BC6H compressed texture format (UF)
            CMP_FORMAT_BC6H_SF = 0x1061, // DXGI_FORMAT_BC6H_SF16 VK_FORMAT_BC6H_SFLOAT_BLOCK GL_COMPRESSED_RGB_BPTC_SIGNED_FLOAT   BC6H compressed texture format (SF)
            CMP_FORMAT_BC7 = 0x0071, // DXGI_FORMAT_BC7_UNORM VK_FORMAT_BC7_UNORM_BLOCK GL_COMPRESSED_RGBA_BPTC_UNORM BC7  compressed texture format

            CMP_FORMAT_ATI1N = 0x0141, // DXGI_FORMAT_BC4_UNORM VK_FORMAT_BC4_UNORM_BLOCK GL_COMPRESSED_RED_RGTC1 Single component

            // compression format using the same technique as DXT5 alpha. Four bits per pixel.
            CMP_FORMAT_ATI2N = 0x0151, // DXGI_FORMAT_BC5_UNORM VK_FORMAT_BC5_UNORM_BLOCK GL_COMPRESSED_RG_RGTC2 Two component compression format using the same

            // technique as DXT5 alpha. Designed for compression of tangent space normal maps. Eight bits per pixel.
            CMP_FORMAT_ATI2N_XY = 0x0152, // DXGI_FORMAT_BC5_UNORM VK_FORMAT_BC5_UNORM_BLOCK GL_COMPRESSED_RG_RGTC2 Two component compression format using the

            // same technique as DXT5 alpha. The same as ATI2N but with the channels swizzled. Eight bits per pixel.
            CMP_FORMAT_ATI2N_DXT5 = 0x0153, // DXGI_FORMAT_BC5_UNORM VK_FORMAT_BC5_UNORM_BLOCK GL_COMPRESSED_RG_RGTC2 ATI2N like format

            // using DXT5. Intended for use on GPUs that do not natively support ATI2N. Eight bits per pixel.

            CMP_FORMAT_DXT1 = 0x0211, // DXGI_FORMAT_BC1_UNORM VK_FORMAT_BC1_RGB_UNORM_BLOCK GL_COMPRESSED_RGBA_S3TC_DXT1_EXT

            // A DXTC compressed texture matopaque (or 1-bit alpha). Four bits per pixel.
            CMP_FORMAT_DXT3 = 0x0221, // DXGI_FORMAT_BC2_UNORM VK_FORMAT_BC2_UNORM_BLOCK GL_COMPRESSED_RGBA_S3TC_DXT3_EXT

            // DXTC compressed texture format with explicit alpha. Eight bits per pixel.

            CMP_FORMAT_DXT5 = 0x0231, // DXGI_FORMAT_BC3_UNORM VK_FORMAT_BC3_UNORM_BLOCK GL_COMPRESSED_RGBA_S3TC_DXT5_EXT

            // DXTC compressed texture format with interpolated alpha. Eight bits per pixel.
            CMP_FORMAT_DXT5_xGBR = 0x0252, // DXGI_FORMAT_UNKNOWN DXT5 with the red component swizzled into the alpha channel. Eight bits per pixel.
            CMP_FORMAT_DXT5_RxBG = 0x0253, // DXGI_FORMAT_UNKNOWN swizzled DXT5 format with the green component swizzled into the alpha channel. Eight bits per pixel.
            CMP_FORMAT_DXT5_RBxG = 0x0254, // DXGI_FORMAT_UNKNOWN swizzled DXT5 format with the green component swizzled

            // into the alpha channel & the blue component swizzled into the green channel. Eight bits per pixel.
            CMP_FORMAT_DXT5_xRBG = 0x0255, // DXGI_FORMAT_UNKNOWN swizzled DXT5 format with the green component swizzled into

            // the alpha channel & the red component swizzled into the green channel. Eight bits per pixel.
            CMP_FORMAT_DXT5_RGxB = 0x0256, // DXGI_FORMAT_UNKNOWN swizzled DXT5 format with the blue component swizzled into the alpha channel. Eight bits per pixel.
            CMP_FORMAT_DXT5_xGxR = 0x0257, // two-component swizzled DXT5 format with the red component swizzled into the alpha channel &

            // the green component in the green channel. Eight bits per pixel.

            CMP_FORMAT_ATC_RGB = 0x0301, // CMP - a compressed RGB format.
            CMP_FORMAT_ATC_RGBA_Explicit = 0x0302, // CMP - a compressed ARGB format with explicit alpha.
            CMP_FORMAT_ATC_RGBA_Interpolated = 0x0303, // CMP - a compressed ARGB format with interpolated alpha.

            CMP_FORMAT_ASTC = 0x0A01, // DXGI_FORMAT_UNKNOWN   VK_FORMAT_ASTC_4x4_UNORM_BLOCK to VK_FORMAT_ASTC_12x12_UNORM_BLOCK
            CMP_FORMAT_APC = 0x0A02, // APC Texture Compressor
            CMP_FORMAT_PVRTC = 0x0A03, //

            CMP_FORMAT_ETC_RGB = 0x0E01, // DXGI_FORMAT_UNKNOWN VK_FORMAT_ETC2_R8G8B8_UNORM_BLOCK GL_COMPRESSED_RGB8_ETC2  backward compatible
            CMP_FORMAT_ETC2_RGB = 0x0E02, // DXGI_FORMAT_UNKNOWN VK_FORMAT_ETC2_R8G8B8_UNORM_BLOCK GL_COMPRESSED_RGB8_ETC2
            CMP_FORMAT_ETC2_SRGB = 0x0E03, // DXGI_FORMAT_UNKNOWN VK_FORMAT_ETC2_R8G8B8_SRGB_BLOCK GL_COMPRESSED_SRGB8_ETC2
            CMP_FORMAT_ETC2_RGBA = 0x0E04, // DXGI_FORMAT_UNKNOWN VK_FORMAT_ETC2_R8G8B8A8_UNORM_BLOCK GL_COMPRESSED_RGBA8_ETC2_EAC
            CMP_FORMAT_ETC2_RGBA1 = 0x0E05, // DXGI_FORMAT_UNKNOWN VK_FORMAT_ETC2_R8G8B8A1_UNORM_BLOCK GL_COMPRESSED_RGB8_PUNCHTHROUGH_ALPHA1_ETC2
            CMP_FORMAT_ETC2_SRGBA = 0x0E06, // DXGI_FORMAT_UNKNOWN VK_FORMAT_ETC2_R8G8B8A8_SRGB_BLOCK GL_COMPRESSED_SRGB8_ALPHA8_ETC2_EAC
            CMP_FORMAT_ETC2_SRGBA1 = 0x0E07, // DXGI_FORMAT_UNKNOWN VK_FORMAT_ETC2_R8G8B8A1_SRGB_BLOCK GL_COMPRESSED_SRGB8_PUNCHTHROUGH_ALPHA1_ETC2

            // New Compression Formats -------------------------------------------------------------
            CMP_FORMAT_BINARY = 0x0B01, //< Binary/Raw Data Format
            CMP_FORMAT_GTC = 0x0B02, //< GTC   Fast Gradient Texture Compressor
            CMP_FORMAT_BASIS = 0x0B03, //< BASIS compression

            CMP_FORMAT_MAX = 0xFFFF // Invalid Format
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [SuppressMessage("ReSharper", "ArrangeTypeMemberModifiers")]
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct Adjustments
        {
            public bool use;
            public double brightness;
            public double contrast;
            public double gamma;

            public static Adjustments NewEmpty() =>
                new ()
                {
                    use = false,
                    brightness = 0,
                    contrast = 0,
                    gamma = 0,
                };
        }

        internal enum FreeImageColorType : int
        {
            Miniswhite = 0, //! min value is white
            Minisblack = 1, //! min value is black
            RGB = 2, //! RGB color model
            Palette = 3, //! color map indexed
            Rgbalpha = 4, //! RGB color model with alpha channel
            Cmyk = 5 //! CMYK color model
        }

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = PREFIX + "initialize")]
        internal extern static ImageResult TexturesFuseInitialize(InitOptions initOptions, out IntPtr context);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = PREFIX + "dispose")]
        internal extern static ImageResult TexturesFuseDispose(IntPtr context);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = PREFIX + "release")]
        internal extern static ImageResult TexturesFuseRelease(IntPtr context, IntPtr handle);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = PREFIX + "processed_image_from_memory")]
        internal extern static unsafe ImageResult TexturesFuseProcessedImageFromMemory(
            IntPtr context,
            byte* bytes,
            int bytesLength,
            int maxSideLength,
            out byte* outputBytes,
            out uint width,
            out uint height,
            out uint bitsPerPixel,
            out FreeImageColorType colorType,
            out IntPtr releaseHandle
        );

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = PREFIX + "astc_image_from_memory")]
        internal extern static unsafe ImageResult TexturesFuseASTCImageFromMemory(
            IntPtr context,
            Swizzle swizzle,
            byte* bytes,
            int bytesLength,
            int maxSideLength,
            Adjustments adjustments,
            out byte* outputBytes,
            out int outputLength,
            out uint width,
            out uint height,
            out IntPtr releaseHandle
        );

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = PREFIX + "cmp_image_from_memory")]
        internal extern static unsafe ImageResult TexturesFuseCMPImageFromMemory(
            IntPtr context,
            byte* bytes,
            int bytesLength,
            int maxSideLength,
            CMP_FORMAT cmpFormat,
            CMP_CompressOptions compressOptions,
            out byte* outputBytes,
            out int outputLength,
            out uint width,
            out uint height,
            out IntPtr releaseHandle
        );
    }
}
