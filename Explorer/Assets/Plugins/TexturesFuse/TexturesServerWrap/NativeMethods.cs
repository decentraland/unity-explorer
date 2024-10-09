using System;
using System.Runtime.InteropServices;

namespace Plugins.RustEthereum.SignServerWrap
{
    public static class NativeMethods
    {
        private const string LIBRARY_NAME = "libtexturesfuse";

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = "texturesfuse_initialize")]
        internal extern static bool TexturesFuseInitialize();

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = "texturesfuse_dispose")]
        internal extern static bool TexturesFuseDispose();

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = "texturesfuse_release")]
        internal extern static void TexturesFuseRelease(IntPtr handle);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = "texturesfuse_processed_image_from_memory")]
        internal extern static unsafe IntPtr TexturesFuseProcessedImageFromMemory(
            byte* bytes,
            int length,
            out byte* outputBytes,
            out int outputLength
        );
    }
}
