#nullable enable

using System;
using System.IO;

namespace ECS.StreamableLoading.AssetBundles
{
    /// <summary>
    ///     Locations and maintenance of the abgen sidecar's on-disk bundle stores under
    ///     <c>persistentDataPath</c>, one home per server flavor.
    /// </summary>
    public static class AbgenBundleDiskCache
    {
        /// <summary>Sidecar server home under persistentDataPath: bin/ (the binary), cache/ and out/ (bundles).</summary>
        public const string SIDECAR_DIR = "abgen";

        /// <summary>Sidecar home for the local-scene-development flavor; kept apart from the catalyst one.</summary>
        public const string SIDECAR_LSD_DIR = "abgen-lsd";

        /// <summary>
        ///     Every disk root the sidecar writes bundles to: each flavor's cache/ and out/ directories —
        ///     deliberately never a sidecar's bin/, which holds the downloaded binary. Reads
        ///     persistentDataPath; call from the main thread.
        /// </summary>
        public static string[] AllBundleRoots()
        {
            string persistent = UnityEngine.Application.persistentDataPath;

            return new[]
            {
                Path.Combine(persistent, SIDECAR_DIR, "cache"),
                Path.Combine(persistent, SIDECAR_DIR, "out"),
                Path.Combine(persistent, SIDECAR_LSD_DIR, "cache"),
                Path.Combine(persistent, SIDECAR_LSD_DIR, "out"),
            };
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

        public struct ClearResult
        {
            public int DeletedFiles;
            public long DeletedBytes;
            public int SkippedFiles;
        }
    }
}
