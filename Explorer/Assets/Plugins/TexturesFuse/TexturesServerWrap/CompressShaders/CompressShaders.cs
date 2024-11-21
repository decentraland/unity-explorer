using Cysharp.Threading.Tasks;
using DCL.Platforms;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using Utility;

namespace Plugins.TexturesFuse.TexturesServerWrap.CompressShaders
{
    public class CompressShaders : ICompressShaders
    {
        private readonly Func<ITexturesFuse> texturesFuseFactory;
        private readonly IPlatform platformInfo;

        private const string DIR = ICompressShaders.PLUGINS_PATH;
        private const string EXTENSION = ".cmp";

        private static readonly byte[] EMPTY_PNG =
        {
            0X89, 0X50, 0X4E, 0X47, 0X0D, 0X0A, 0X1A, 0X0A, 0X00, 0X00, 0X00, 0X0D, 0X49, 0X48, 0X44, 0X52, 0X00, 0X00, 0X00, 0X04, 0X00, 0X00, 0X00, 0X04, 0X08, 0X02, 0X00, 0X00, 0X00, 0X26, 0X93, 0X09, 0X29, 0X00, 0X00, 0X00, 0X01, 0X73, 0X52, 0X47, 0X42, 0X01, 0XD9, 0XC9, 0X2C, 0X7F, 0X00, 0X00, 0X00, 0X09, 0X70, 0X48, 0X59, 0X73, 0X00, 0X00, 0X0B, 0X13, 0X00, 0X00, 0X0B, 0X13, 0X01, 0X00, 0X9A, 0X9C, 0X18, 0X00, 0X00, 0X00, 0X0F, 0X49, 0X44, 0X41, 0X54, 0X78, 0X9C, 0X63, 0XF8, 0X8F, 0X04, 0X18, 0X88, 0XE3, 0X00, 0X00, 0XDB, 0X90, 0X2F, 0XD1, 0X6E, 0X45, 0XB0, 0XB2, 0X00, 0X00, 0X00, 0X00, 0X49, 0X45, 0X4E, 0X44, 0XAE, 0X42, 0X60, 0X82
        };

        public CompressShaders(Func<ITexturesFuse> texturesFuseFactory, IPlatform platformInfo)
        {
            this.texturesFuseFactory = texturesFuseFactory;
            this.platformInfo = platformInfo;
        }

        public bool AreReady()
        {
            if (ShouldIgnorePlatform())
                return true;

            if (Directory.Exists(DIR) == false)
                return false;

            var files = Directory.EnumerateFiles(DIR, $"*.{EXTENSION}", SearchOption.AllDirectories);
            return files.Any();
        }

        public async UniTask WarmUpAsync(CancellationToken token)
        {
            if (AreReady())
                return;

            if (Directory.Exists(DIR) == false)
            {
                Directory.CreateDirectory(DIR);
                CopyFilesRecursively(ICompressShaders.ShadersDir(), DIR);
            }

            using var texturesFuse = texturesFuseFactory()!;

            foreach (TextureType textureType in EnumUtils.Values<TextureType>())
                await texturesFuse.TextureFromBytesAsync(EMPTY_PNG, textureType, token);
        }

        private bool ShouldIgnorePlatform() =>
            platformInfo.IsNot(IPlatform.Kind.Windows);

        private static void CopyFilesRecursively(string sourcePath, string targetPath)
        {
            foreach (string dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(dirPath.Replace(sourcePath, targetPath));

            foreach (string newPath in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
                File.Copy(newPath, newPath.Replace(sourcePath, targetPath), true);
        }
    }
}
