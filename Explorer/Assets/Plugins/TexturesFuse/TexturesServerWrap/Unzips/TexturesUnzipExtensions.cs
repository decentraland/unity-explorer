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
            this ITexturesUnzip unzip,
            byte[] bytes,
            TextureType type,
            CancellationToken token
        )
        {
            using var pinned = new HandleScope(bytes);
            var result = await unzip.TextureFromBytesAsync(pinned.Addr, bytes.Length, type, token);
            return result;
        }

        public static LogTexturesUnzip WithLog(this ITexturesUnzip texturesUnzip, string prefix) =>
            new (texturesUnzip, prefix);
    }
}
