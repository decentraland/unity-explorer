using System;
using System.Runtime.InteropServices;

namespace Plugins.TexturesFuse.TexturesServerWrap
{
    public static class NativeMethods
    {
        private const string LIBRARY_NAME = "texturesfuse";
        private const string PREFIX = "texturesfuse_";

        internal enum ImageResult : int
        {
            ErrorUnknown = 0,
            Success = 1,
            ErrorOpenMemoryStream = 2,
            ErrorUnknownImageFormat = 3,
            ErrorCannotLoadImage = 4,
            ErrorCannotGetBits = 5
        }

        internal enum FreeImageColorType : int {
            Miniswhite = 0,		//! min value is white
            Minisblack = 1,		//! min value is black
            RGB        = 2,		//! RGB color model
            Palette    = 3,		//! color map indexed
            Rgbalpha   = 4,		//! RGB color model with alpha channel
            Cmyk       = 5		//! CMYK color model
        }

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = PREFIX + "initialize")]
        internal extern static bool TexturesFuseInitialize();

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = PREFIX + "dispose")]
        internal extern static bool TexturesFuseDispose();

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = PREFIX + "release")]
        internal extern static void TexturesFuseRelease(IntPtr handle);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = PREFIX + "processed_image_from_memory")]
        internal extern static unsafe ImageResult TexturesFuseProcessedImageFromMemory(
            byte* bytes,
            uint length,
            out byte* outputBytes,
            out uint width,
            out uint height,
            out uint bitsPerPixel,
            out FreeImageColorType colorType,
            out IntPtr releaseHandle
        );
    }
}
