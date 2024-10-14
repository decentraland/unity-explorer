using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using System;
using System.Diagnostics;
using System.Threading;
using Utility.Types;

namespace Plugins.TexturesFuse.TexturesServerWrap.Unzips
{
    public class LogTexturesUnzip : ITexturesUnzip
    {
        private readonly ITexturesUnzip origin;
        private readonly Stopwatch stopwatch;
        private ulong index;

        public LogTexturesUnzip(ITexturesUnzip origin)
        {
            this.origin = origin;
            stopwatch = new Stopwatch();
        }

        public void Dispose()
        {
            origin.Dispose();
        }

        public async UniTask<EnumResult<OwnedTexture2D, NativeMethods.ImageResult>> TextureFromBytesAsync(ReadOnlyMemory<byte> bytes, CancellationToken token)
        {
            ulong i = index++;
            stopwatch.Restart();
            ReportHub.Log(ReportCategory.TEXTURES, $"TexturesUnzip: start decompress {i}");
            var result = await origin.TextureFromBytesAsync(bytes, token);
            stopwatch.Stop();
            ReportHub.Log(ReportCategory.TEXTURES, $"TexturesUnzip: end decompress {i} with time spent: {stopwatch.ElapsedMilliseconds} ms");
            return result;
        }
    }
}
