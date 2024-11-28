using Cysharp.Threading.Tasks;
using System.Linq;
using System.Threading;

namespace Plugins.TexturesFuse.TexturesServerWrap.CompressShaders
{
    public class SeveralCompressShaders : ICompressShaders
    {
        private readonly ICompressShaders[] compressShaders;

        public SeveralCompressShaders(params ICompressShaders[] compressShaders)
        {
            this.compressShaders = compressShaders;
        }

        public bool AreReady()
        {
            return compressShaders.All(e => e.AreReady());
        }

        public async UniTask WarmUpAsync(CancellationToken token)
        {
            foreach (ICompressShaders compressShader in compressShaders)
                if (compressShader.AreReady() == false)
                    await compressShader.WarmUpAsync(token);
        }
    }
}
