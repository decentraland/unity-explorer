using Cysharp.Threading.Tasks;
using System.IO;
using System.Threading;
using UnityEngine;

namespace Plugins.TexturesFuse.TexturesServerWrap.CompressShaders
{
    public interface ICompressShaders
    {
        const string CMD_ARGS = "compile-shaders-and-exit";

        bool AreReady();

        UniTask WarmUpAsync(CancellationToken token);

        static string ShadersDir() =>
            Application.isEditor
                ? Path.Combine(Directory.GetCurrentDirectory(), "Assets/StreamingAssets/plugins")
                : "Decentraland_Data/StreamingAssets/plugins";
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
