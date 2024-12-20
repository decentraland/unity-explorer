using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
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

        public async UniTask<EnumResult<IOwnedTexture2D, NativeMethods.ImageResult>> TextureFromBytesAsync(ITexturesFuse.ImageData imageData, CancellationToken token)
        {
            string tagString = imageData.tag != null ? $"; with tag: {imageData.tag}" : string.Empty;

            ulong i = index++;
            stopwatch.Restart();
            ReportHub.Log(ReportCategory.TEXTURES, $"TexturesUnzip - {prefix}: start decompress {i}" + tagString);
            var result = await origin.TextureFromBytesAsync(imageData, token);
            stopwatch.Stop();
            ReportHub.Log(ReportCategory.TEXTURES, $"TexturesUnzip - {prefix}: end decompress {i} with time spent: {stopwatch.ElapsedMilliseconds} ms" + tagString);

            if (result.Success == false)
                ReportHub.LogError(ReportCategory.TEXTURES, $"TexturesUnzip - {prefix}: decompress {i} failed with error: {result}" + tagString);

            return result;
        }
    }
}
