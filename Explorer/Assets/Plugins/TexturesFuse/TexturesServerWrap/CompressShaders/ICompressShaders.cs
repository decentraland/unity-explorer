using Cysharp.Threading.Tasks;
using DCL.Platforms;
using DCL.SceneLoadingScreens.SplashScreen;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using System;
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
            char ps = CompressShadersExtensions.PATH_SEPARATOR;

            return Application.isEditor
                ? $"Assets{ps}StreamingAssets{ps}plugins"
                : $"Decentraland_Data{ps}StreamingAssets{ps}plugins";
        }

        static string NodeDir()
        {
            char ps = CompressShadersExtensions.PATH_SEPARATOR;

            return Application.isEditor
                ? $"Assets{ps}StreamingAssets{ps}fusenode"
                : $"Decentraland_Data{ps}StreamingAssets{ps}fusenode";
        }

        static ICompressShaders NewDefault(Func<ITexturesFuse> texturesFuseFactory, IPlatform platformInfo) =>
            new PlatformFilterCompressShaders(
                new SeveralCompressShaders(
                    new NodeCompressShaders(),
                    new CompressShaders(texturesFuseFactory, platformInfo)
                ),
                platformInfo
            );
    }

    public static class CompressShadersExtensions
    {
        public static readonly char PATH_SEPARATOR = IPlatform.DEFAULT.Is(IPlatform.Kind.Windows) ? '\\' : '/';

        public static UniTask WarmUpIfRequiredAsync(this ICompressShaders compressShaders, CancellationToken token) =>
            compressShaders.AreReady()
                ? UniTask.CompletedTask
                : compressShaders.WarmUpAsync(token);

        public static LogCompressShaders WithLog(this ICompressShaders origin, string prefix) =>
            new (origin, prefix);

        public static SplashScreenCompressShaders WithSplashScreen(this ICompressShaders origin, ISplashScreen splashScreen, bool hideOnFinish) =>
            new (origin, splashScreen, hideOnFinish);

        public static void CopyFilesRecursively(string sourcePath, string targetPath)
        {
            foreach (string dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(dirPath.Replace(sourcePath, targetPath));

            foreach (string newPath in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
                File.Copy(newPath, newPath.Replace(sourcePath, targetPath), true);
        }
    }
}
