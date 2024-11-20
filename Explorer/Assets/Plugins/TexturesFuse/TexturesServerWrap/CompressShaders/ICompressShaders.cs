using Cysharp.Threading.Tasks;
using DCL.Platforms;
using System.IO;
using System.Threading;
using UnityEngine;

namespace Plugins.TexturesFuse.TexturesServerWrap.CompressShaders
{
    public interface ICompressShaders
    {
        const string CMD_ARGS = "compile-shaders-and-exit";

        const string PLUGINS_PATH = "plugins";

        bool AreReady();

        UniTask WarmUpAsync(CancellationToken token);

        static string ShadersDir()
        {
            char ps = IPlatform.DEFAULT.Is(IPlatform.Kind.Windows) ? '\\' : '/';

            return Application.isEditor
                ? $"Assets{ps}StreamingAssets{ps}plugins"
                : $"Decentraland_Data{ps}StreamingAssets{ps}plugins";
        }
    }

    public static class CompressShadersExtensions
    {
        public static UniTask WarmUpIfRequiredAsync(this ICompressShaders compressShaders, CancellationToken token) =>
            compressShaders.AreReady()
                ? UniTask.CompletedTask
                : compressShaders.WarmUpAsync(token);

        public static LogCompressShaders WithLog(this ICompressShaders origin, string prefix) =>
            new (origin, prefix);
    }
}
