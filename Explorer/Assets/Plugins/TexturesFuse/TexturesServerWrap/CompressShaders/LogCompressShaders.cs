using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using System.Threading;

namespace Plugins.TexturesFuse.TexturesServerWrap.CompressShaders
{
    public class LogCompressShaders : ICompressShaders
    {
        private readonly ICompressShaders origin;
        private readonly string prefix;

        public LogCompressShaders(ICompressShaders origin, string prefix)
        {
            this.origin = origin;
            this.prefix = prefix;
        }

        public bool AreReady()
        {
            bool result = origin.AreReady();
            ReportHub.Log(ReportCategory.TEXTURES, $"{prefix} - CompressShaders AreReady: {result}");
            return result;
        }

        public async UniTask WarmUpAsync(CancellationToken token)
        {
            ReportHub.Log(ReportCategory.TEXTURES, $"{prefix} - CompressShaders WarmUpAsync Start");
            await origin.WarmUpAsync(token);
            ReportHub.Log(ReportCategory.TEXTURES, $"{prefix} - CompressShaders WarmUpAsync Finish");
        }
    }
}
