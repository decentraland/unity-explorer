using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.Hashing;
using ECS.StreamableLoading.Cache.Disk;
using System;
using System.IO;
using System.Threading;
using UnityEngine;

namespace ECS.StreamableLoading.Fonts
{
    // FreeType re-reads the file whenever the atlas needs glyphs: files stay on disk while their assets live
    public class FontFileStore
    {
        public const int MAX_FILE_BYTES = 16 * 1024 * 1024;

        private const string DIRECTORY_NAME = "SceneFonts";

        private const int SFNT_HEADER_BYTES = 12;

        private const uint SFNT_VERSION_TRUE_TYPE = 0x00010000;
        private const uint SFNT_VERSION_CFF = 0x4F54544F;
        private const uint SFNT_VERSION_APPLE_TRUE_TYPE = 0x74727565;
        private const uint SFNT_VERSION_COLLECTION = 0x74746366;

        private readonly string directory;

        public FontFileStore(string directory)
        {
            this.directory = directory;
        }

        public static FontFileStore InPersistentData()
        {
            var store = new FontFileStore(Path.Combine(Application.persistentDataPath, DIRECTORY_NAME));
            store.Clear();
            return store;
        }

        public static bool LooksLikeFontFile(byte[] bytes)
        {
            if (bytes.Length < SFNT_HEADER_BYTES)
                return false;

            uint version = ReadVersion(bytes);

            return version is SFNT_VERSION_TRUE_TYPE
                           or SFNT_VERSION_CFF
                           or SFNT_VERSION_APPLE_TRUE_TYPE
                           or SFNT_VERSION_COLLECTION;
        }

        public void Clear()
        {
            if (!Directory.Exists(directory))
                return;

            try { Directory.Delete(directory, true); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                ReportHub.LogWarning(ReportCategory.SDK_FONTS, $"The scene fonts under {directory} could not be removed: {e.Message}");
            }
        }

        public async UniTask<string> StoreAsync(byte[] bytes, CancellationToken ct)
        {
            await UniTask.SwitchToThreadPool();
            ct.ThrowIfCancellationRequested();

            string path = Path.Combine(directory, FileName(bytes));

            if (File.Exists(path))
                return path;

            Directory.CreateDirectory(directory);

            string temp = $"{path}.{Guid.NewGuid():N}.tmp";

            try
            {
                File.WriteAllBytes(temp, bytes);

                try { File.Move(temp, path); }
                catch (IOException) when (File.Exists(path))
                {
                    // Another download of the same bytes won the rename
                }
            }
            finally
            {
                if (File.Exists(temp))
                    File.Delete(temp);
            }

            return path;
        }

        private static string FileName(byte[] bytes)
        {
            using HashKey key = HashKey.FromOwnedMemory(SHA256Hashing.ComputeHash(bytes));
            string extension = bytes.Length < SFNT_HEADER_BYTES ? ".ttf" : ReadVersion(bytes) switch
            {
                SFNT_VERSION_CFF => ".otf",
                SFNT_VERSION_COLLECTION => ".ttc",
                _ => ".ttf",
            };

            return HashNamings.HashNameFrom(key, extension);
        }

        private static uint ReadVersion(byte[] bytes) =>
            ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
    }
}
