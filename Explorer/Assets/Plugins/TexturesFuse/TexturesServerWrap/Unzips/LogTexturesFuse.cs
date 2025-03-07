using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using System;
using System.Diagnostics;
using System.Threading;
using Utility.Types;

namespace Plugins.TexturesFuse.TexturesServerWrap.Unzips
{
    public class LogTexturesFuse : ITexturesFuse
    {
        private readonly ITexturesFuse origin;
        private readonly Stopwatch stopwatch;
        private readonly string prefix;
        private ulong index;

        public LogTexturesFuse(ITexturesFuse origin, string prefix)
        {
            this.origin = origin;
            this.prefix = prefix;
            stopwatch = new Stopwatch();
        }

        public void Dispose()
        {
            origin.Dispose();
        }

        public async UniTask<EnumResult<IOwnedTexture2D, NativeMethods.ImageResult>> TextureFromBytesAsync(IntPtr bytes, int bytesLength, TextureType type, CancellationToken token)
        {
            ulong i = index++;
            stopwatch.Restart();
            ReportHub.Log(ReportCategory.TEXTURES, $"TexturesUnzip - {prefix}: start decompress {i}");
            var result = await origin.TextureFromBytesAsync(bytes, bytesLength, type, token);
            stopwatch.Stop();
            ReportHub.Log(ReportCategory.TEXTURES, $"TexturesUnzip - {prefix}: end decompress {i} with time spent: {stopwatch.ElapsedMilliseconds} ms");

            if (result.Success == false)
                ReportHub.LogError(ReportCategory.TEXTURES, $"TexturesUnzip - {prefix}: decompress {i} failed with error: {result}");

            return result;
        }
    }
}
