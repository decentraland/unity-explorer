using System;
using System.Runtime.InteropServices;

namespace Plugins.TexturesFuse.TexturesServerWrap
{
    public static class NativeMethods
    {
        private const string LIBRARY_NAME = "texturesfuse";
        private const string PREFIX = "texturesfuse_";

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = PREFIX + "initialize")]
        internal extern static bool TexturesFuseInitialize();

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = PREFIX + "dispose")]
        internal extern static bool TexturesFuseDispose();

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = PREFIX + "release")]
        internal extern static void TexturesFuseRelease(IntPtr handle);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = PREFIX + "processed_image_from_memory")]
        internal extern static unsafe IntPtr TexturesFuseProcessedImageFromMemory(
            byte* bytes,
            int length,
            out byte* outputBytes,
            out int outputLength
        );
    }
}
