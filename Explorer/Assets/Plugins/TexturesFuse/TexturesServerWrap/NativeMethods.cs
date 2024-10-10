using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Plugins.TexturesFuse.TexturesServerWrap
{
    [SuppressMessage("ReSharper", "EnumUnderlyingTypeIsInt")]
    public static class NativeMethods
    {
        private const string LIBRARY_NAME = "libtexturesfuse";
        private const string PREFIX = "texturesfuse_";

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [SuppressMessage("ReSharper", "ArrangeTypeMemberModifiers")]
        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct InitOptions
        {
#pragma region ASTC_options

            public int ASTCProfile;
            public uint blockX;
            public uint blockY;
            public uint blockZ;
            public float quality;
            public uint flags;
#pragma endregion ASTC_options
        }

        internal enum ImageResult : int
        {
            ErrorUnknown = 0,
            Success = 1,
            ErrorOpenMemoryStream = 2,
            ErrorUnknownImageFormat = 3,
            ErrorCannotLoadImage = 4,
            ErrorCannotGetBits = 5,
            ErrorCannotDownscale = 6,

            ErrorInvalidPointer = 10,
            ErrorASTCOnInit = 11,

            ErrorDisposeAlreadyDisposed = 20,
            ErrorDisposeNotAllTexturesReleased = 21,

            ErrorReleaseNoHandleFound = 30,
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
            byte* bytes,
            int bytesLength,
            int maxSideLength,
            out byte* outputBytes,
            out int outputLength,
            out uint width,
            out uint height,
            out IntPtr releaseHandle
        );
    }
}
