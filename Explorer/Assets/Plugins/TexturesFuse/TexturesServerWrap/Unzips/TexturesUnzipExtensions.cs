using Cysharp.Threading.Tasks;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using Utility.Types;

namespace Plugins.TexturesFuse.TexturesServerWrap.Unzips
{
    public static class TexturesUnzipExtensions
    {
        private struct HandleScope : IDisposable
        {
            private GCHandle handle;

            public IntPtr Addr => handle.AddrOfPinnedObject();

            public HandleScope(object obj)
            {
                handle = GCHandle.Alloc(obj, GCHandleType.Pinned);
            }

            public void Dispose()
            {
                handle.Free();
            }
        }

        public static async UniTask<EnumResult<IOwnedTexture2D, NativeMethods.ImageResult>> TextureFromBytesAsync(
            this ITexturesFuse fuse,
            byte[] bytes,
            TextureType type,
            CancellationToken token,
            string? tag
        )
        {
            using var pinned = new HandleScope(bytes);
            var imageData = new ITexturesFuse.ImageData(pinned.Addr, bytes.Length, type, tag);
            var result = await fuse.TextureFromBytesAsync(imageData, token);
            return result;
        }

        public static LogTexturesFuse WithLog(this ITexturesFuse texturesFuse, string prefix) =>
            new (texturesFuse, prefix);
    }
}
