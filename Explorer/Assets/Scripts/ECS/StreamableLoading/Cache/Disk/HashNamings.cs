using DCL.Optimization.ThreadSafePool;
using DCL.Utilities.Extensions;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ECS.StreamableLoading.Cache.Disk
{
    public static class HashNamings
    {
        private const int SHA256_HASH_LENGTH = 32; //sha256 produces 32 bits
        private static readonly IReadOnlyDictionary<byte, string> CACHED_SYMBOLS;

        private static readonly ArrayPool<byte> MEMORY_POOL = ArrayPool<byte>.Shared!;
        private static readonly SHA256 SHA256 = SHA256.Create()!;
        private static readonly ThreadSafeObjectPool<StringBuilder> STRING_BUILDER_POOL = new (
            () => new StringBuilder(SHA256_HASH_LENGTH * 2), //because in dex each byte takes 2 symbols
            actionOnRelease: sb => sb.Clear()
        );

        static HashNamings()
        {
            var dictionary = new Dictionary<byte, string>();

            for (var i = 0; i <= byte.MaxValue; i++)
                dictionary[(byte)i] = i.ToString("x2");

            CACHED_SYMBOLS = dictionary;
        }

        public static string HashNameFrom(string key, string extension)
        {
            int maxByteCount = Encoding.UTF8.GetMaxByteCount(key.Length);
            using var ownedMemory = OwnedMemory.FromPool(MEMORY_POOL, maxByteCount);
            int byteCount = Encoding.UTF8.GetBytes(key, 0, key.Length, ownedMemory.Memory, 0);

            Span<byte> hash = stackalloc byte[SHA256_HASH_LENGTH];

            if (SHA256.TryComputeHash(ownedMemory.Memory.AsSpan(0, byteCount), hash, out _) == false)
                throw new Exception("Something went wrong during hash computation");

            using var __ = STRING_BUILDER_POOL.Get(out var sb);

            foreach (byte b in hash)
                sb.Append(CACHED_SYMBOLS[b]!);

            var hashStr = sb.ToString();
            string path = Path.ChangeExtension(hashStr, extension);
            return path;
        }

        private readonly struct OwnedMemory : IDisposable
        {
            public readonly byte[] Memory;
            private readonly ArrayPool<byte> pool;

            private OwnedMemory(byte[] memory, ArrayPool<byte> pool)
            {
                this.Memory = memory;
                this.pool = pool;
            }

            public static OwnedMemory FromPool(ArrayPool<byte> pool, int length)
            {
                byte[] memory = pool.Rent(length).EnsureNotNull("Cannot rent memory");
                return new OwnedMemory(memory, pool);
            }

            public void Dispose()
            {
                pool.Return(Memory);
            }
        }
    }
}
