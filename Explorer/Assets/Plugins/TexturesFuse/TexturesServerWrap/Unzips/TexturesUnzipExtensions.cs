using Cysharp.Threading.Tasks;
using System;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Threading;
using Utility.Types;

namespace Plugins.TexturesFuse.TexturesServerWrap.Unzips
{
    public static class TexturesUnzipExtensions
    {
        public static async UniTask<EnumResult<IOwnedTexture2D, NativeMethods.ImageResult>> TextureFromBytesAsync(
            this ITexturesFuse fuse,
            Memory<byte> bytes,
            TextureType type,
            CancellationToken token
        )
        {
            using MemoryHandle pinned = bytes.Pin();
            IntPtr ptr;

            unsafe { ptr = (IntPtr)pinned.Pointer; }

            EnumResult<IOwnedTexture2D, NativeMethods.ImageResult> result = await fuse.TextureFromBytesAsync(ptr, bytes.Length, type, token);
            return result;
        }

        public static LogTexturesFuse WithLog(this ITexturesFuse texturesFuse, string prefix) =>
            new (texturesFuse, prefix);
    }
}
