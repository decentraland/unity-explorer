using DCL.Diagnostics;
using DCL.Optimization.ThreadSafePool;
using System.Collections.Generic;
using System.Text;

namespace ECS.StreamableLoading.Cache.Disk
{
    public static class HashNamings
    {
        private static readonly IReadOnlyDictionary<byte, string> CACHED_SYMBOLS;

        private static readonly ThreadSafeObjectPool<StringBuilder> STRING_BUILDER_POOL = new (
            () => new StringBuilder(DiskHashing.SHA256_HASH_LENGTH * 2), //because in dex each byte takes 2 symbols
            actionOnRelease: sb => sb.Clear()
        );

        static HashNamings()
        {
            var dictionary = new Dictionary<byte, string>();

            for (var i = 0; i <= byte.MaxValue; i++)
                dictionary[(byte)i] = i.ToString("x2");

            CACHED_SYMBOLS = dictionary;
        }

        public static string HashNameFrom(HashKey key, string extension)
        {
            using var __ = STRING_BUILDER_POOL.Get(out var sb);

            foreach (byte b in key.Hash.Memory)
                sb.Append(CACHED_SYMBOLS[b]!);

            if (extension.StartsWith('.') == false)
                sb.Append('.');

            sb.Append(extension);
            var path = sb.ToString();

            ReportHub.Log(ReportCategory.DISK_CACHE, $"Hash name from key to {path}");

            return path;
        }
    }
}
