using Cysharp.Threading.Tasks;
using DCL.Platforms;
using System.Threading;

namespace Plugins.TexturesFuse.TexturesServerWrap.CompressShaders
{
    internal class PlatformFilterCompressShaders : ICompressShaders
    {
        private readonly ICompressShaders origin;
        private readonly IPlatform platform;

        public PlatformFilterCompressShaders(ICompressShaders origin, IPlatform platform)
        {
            this.origin = origin;
            this.platform = platform;
        }

        public bool AreReady() =>
            ShouldIgnorePlatform() || origin.AreReady();

        public UniTask WarmUpAsync(CancellationToken token) =>
            ShouldIgnorePlatform() ? UniTask.CompletedTask : origin.WarmUpAsync(token);

        private bool ShouldIgnorePlatform() =>
            platform.IsNot(IPlatform.Kind.Windows);
    }
}
