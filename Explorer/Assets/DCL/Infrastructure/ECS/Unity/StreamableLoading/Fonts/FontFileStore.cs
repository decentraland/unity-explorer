using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.Hashing;
using ECS.StreamableLoading.Cache.Disk;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;
using Utility.Multithreading;

namespace ECS.StreamableLoading.Fonts
{
    // FreeType re-reads the file whenever the atlas needs glyphs: files stay on disk while their assets live
    public class FontFileStore
    {
        public const int MAX_FILE_BYTES = 16 * 1024 * 1024;

        private const string DIRECTORY_NAME = "SceneFonts";

        private const int SFNT_HEADER_BYTES = 12;

        private const uint SFNT_VERSION_TRUE_TYPE = 0x00010000;

        private readonly string directory;
        private readonly Dictionary<string, int> references = new ();

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

        public static bool LooksLikeTrueTypeFont(byte[] bytes)
        {
            if (bytes.Length < SFNT_HEADER_BYTES)
                return false;

            uint version = ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];

            return version == SFNT_VERSION_TRUE_TYPE;
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

        public async UniTask<Lease> StoreAsync(byte[] bytes, CancellationToken ct)
        {
            await DCLTask.SwitchToThreadPool();
            ct.ThrowIfCancellationRequested();

            string path = Path.Combine(directory, FileName(bytes));

            lock (references)
            {
                ct.ThrowIfCancellationRequested();
                Store(bytes, path);
                references.TryGetValue(path, out int count);
                references[path] = count + 1;
                return new Lease(this, path);
            }
        }

        internal static async UniTask ReleaseAfterDestructionAsync(Lease?[] files)
        {
            if (files.Length == 0)
                return;

            // Object.Destroy completes after this frame; native fonts may still read their source until then.
            if (Application.isPlaying)
                await UniTask.NextFrame();

            await DCLTask.SwitchToThreadPool();

            foreach (Lease? file in files)
                file?.Dispose();
        }

        private void Store(byte[] bytes, string path)
        {
            if (File.Exists(path))
                return;

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
        }

        private void Release(string path)
        {
            lock (references)
            {
                int count = references[path];

                if (count > 1)
                {
                    references[path] = count - 1;
                    return;
                }

                references.Remove(path);

                try { File.Delete(path); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    ReportHub.LogWarning(ReportCategory.SDK_FONTS, $"The scene font file {path} could not be removed: {e.Message}");
                }
            }
        }

        private static string FileName(byte[] bytes)
        {
            using HashKey key = HashKey.FromOwnedMemory(SHA256Hashing.ComputeHash(bytes));
            return HashNamings.HashNameFrom(key, ".ttf");
        }

        public sealed class Lease : IDisposable
        {
            private FontFileStore? owner;

            public string Path { get; }

            internal Lease(FontFileStore owner, string path)
            {
                this.owner = owner;
                Path = path;
            }

            public void Dispose() => DCLInterlocked.Exchange(ref owner, null)?.Release(Path);
        }
    }
}
