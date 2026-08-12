#nullable enable

using Decentraland.Abgen;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ECS.StreamableLoading.AssetBundles
{
    /// <summary>
    ///     Content-addressed on-disk store for in-process abgen conversions, so bundles survive both a scene
    ///     <c>/reload</c> and a full client restart and only the assets whose bytes actually changed reconvert.
    ///     <para>
    ///     The key is a digest of the exact converter input — the serialized <see cref="AbgenRequest" /> blob,
    ///     which carries the GLB and every dependency's bytes plus the platform — folded together with the abgen
    ///     version. It therefore changes exactly when a source file, a dependency, the platform or the converter
    ///     changes, and never otherwise. This is deliberately NOT abgen's own deps digest: that is computed over
    ///     the content-table hashes, which in local scene development are path-derived and never change on edit.
    ///     </para>
    /// </summary>
    public static class AbgenBundleDiskCache
    {
        // Bump to invalidate every cached bundle when the C# packaging (request layout, file naming) changes.
        private const string CACHE_VERSION = "v1";

        /// <summary>Sidecar server home under persistentDataPath: bin/ (the binary), cache/ and out/ (bundles).</summary>
        public const string SIDECAR_DIR = "abgen";

        /// <summary>Sidecar home for the local-scene-development flavor; kept apart from the catalyst one.</summary>
        public const string SIDECAR_LSD_DIR = "abgen-lsd";

        /// <summary>Reads <see cref="UnityEngine.Application.persistentDataPath" />; call from the main thread.</summary>
        public static string RootDirectory() =>
            Path.Combine(UnityEngine.Application.persistentDataPath, "abgen-bundles", CACHE_VERSION);

        /// <summary>
        ///     Every disk root the abgen lanes write bundles to: this in-process cache plus each sidecar
        ///     flavor's cache/ and out/ directories — deliberately never a sidecar's bin/, which holds the
        ///     downloaded binary. Reads persistentDataPath; call from the main thread.
        /// </summary>
        public static string[] AllBundleRoots()
        {
            string persistent = UnityEngine.Application.persistentDataPath;

            return new[]
            {
                RootDirectory(),
                Path.Combine(persistent, SIDECAR_DIR, "cache"),
                Path.Combine(persistent, SIDECAR_DIR, "out"),
                Path.Combine(persistent, SIDECAR_LSD_DIR, "cache"),
                Path.Combine(persistent, SIDECAR_LSD_DIR, "out"),
            };
        }

        public static string ComputeKey(byte[] requestBlob)
        {
            using var sha = SHA256.Create();
            sha.TransformBlock(requestBlob, 0, requestBlob.Length, null, 0);

            byte[] version = Encoding.UTF8.GetBytes(AbgenConverter.Version);
            sha.TransformFinalBlock(version, 0, version.Length);

            return ToHex(sha.Hash!);
        }

        public static bool TryGetPath(string root, string key, out string path)
        {
            path = PathForKey(root, key);
            return File.Exists(path);
        }

        /// <summary>Atomic write (temp sibling + move) so a crash mid-write never leaves a half-bundle to load.</summary>
        public static void Write(string root, string key, byte[] data)
        {
            string path = PathForKey(root, key);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            // A populated entry for this content key is already the correct bundle (the key is a digest of the
            // exact input), so an existing file means another conversion won the race — nothing to do.
            if (File.Exists(path)) return;

            string tmp = path + ".tmp" + Guid.NewGuid().ToString("N");
            File.WriteAllBytes(tmp, data);

            try { File.Move(tmp, path); }
            catch (IOException)
            {
                // Lost the race between the check above and the move; our copy is redundant.
                if (File.Exists(tmp)) File.Delete(tmp);
            }
        }

        public static void Delete(string root, string key)
        {
            string path = PathForKey(root, key);
            if (File.Exists(path)) File.Delete(path);
        }

        /// <summary>
        ///     Deletes every cached bundle under <paramref name="root" />, including in-progress temp files.
        ///     Files that cannot be deleted (e.g. memory-mapped by a loaded AssetBundle on Windows) are skipped
        ///     and counted. Walks every shard — call from a background thread.
        /// </summary>
        public static ClearResult ClearAll(string root)
        {
            var result = new ClearResult();

            if (!Directory.Exists(root)) return result;

            foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                try
                {
                    long size = new FileInfo(file).Length;
                    File.Delete(file);
                    result.DeletedFiles++;
                    result.DeletedBytes += size;
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { result.SkippedFiles++; }
            }

            return result;
        }

        /// <summary>Aggregate of <see cref="ClearAll(string)" /> over every root.</summary>
        public static ClearResult ClearAll(string[] roots)
        {
            var total = new ClearResult();

            foreach (string root in roots)
            {
                ClearResult result = ClearAll(root);
                total.DeletedFiles += result.DeletedFiles;
                total.DeletedBytes += result.DeletedBytes;
                total.SkippedFiles += result.SkippedFiles;
            }

            return total;
        }

        // Two-char shard keeps any single directory from collecting every bundle (mirrors abgen's LocalContentStore).
        private static string PathForKey(string root, string key) =>
            Path.Combine(root, key.Substring(0, 2), key);

        private static string ToHex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        public struct ClearResult
        {
            public int DeletedFiles;
            public long DeletedBytes;
            public int SkippedFiles;
        }
    }
}
