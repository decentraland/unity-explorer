using Cysharp.Threading.Tasks;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using System.IO;
using System.Threading;

namespace Plugins.TexturesFuse.TexturesServerWrap.CompressShaders
{
    public class NodeCompressShaders : ICompressShaders
    {
        public bool AreReady() =>
            File.Exists(NodeTexturesFuse.CHILD_PROCESS_NAME);

        public UniTask WarmUpAsync(CancellationToken token)
        {
            if (AreReady())
                return UniTask.CompletedTask;

            CompressShadersExtensions.CopyFilesRecursively(ICompressShaders.NodeDir(), ".");
            return UniTask.CompletedTask;
        }
    }
}
