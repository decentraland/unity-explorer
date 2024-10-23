using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using Utility.Types;

namespace Plugins.TexturesFuse.TexturesServerWrap.Unzips
{
    public class SemaphoreTexturesUnzip : ITexturesUnzip
    {
        private readonly ITexturesUnzip origin;
        private readonly SemaphoreSlim semaphoreSlim;

        public SemaphoreTexturesUnzip(ITexturesUnzip origin)
        {
            this.origin = origin;
            semaphoreSlim = new SemaphoreSlim(1, 1);
        }

        public async UniTask<EnumResult<OwnedTexture2D, NativeMethods.ImageResult>> TextureFromBytesAsync(IntPtr bytes, int bytesLength, TextureType type, CancellationToken token)
        {
            await semaphoreSlim.WaitAsync(token);

            try { return await origin.TextureFromBytesAsync(bytes, bytesLength, type, token); }
            finally { semaphoreSlim.Release(); }
        }

        public void Dispose()
        {
            origin.Dispose();
            semaphoreSlim.Dispose();
        }
    }
}
