using DCL.Utilities.Extensions;
using System;
using System.Buffers;
using System.Text;

namespace DCL.Optimization.Hashing
{
    public readonly struct HashKey : IDisposable
    {
        public readonly OwnedMemory Hash;

        private HashKey(OwnedMemory hash)
        {
            Hash = hash;
        }

        public static HashKey FromString(string key)
        {
            using var keyMemory = OwnedMemory.FromString(key);
            var computedHash = SHA256Hashing.ComputeHash(keyMemory.Memory);
            return new HashKey(computedHash);
        }

        /// <summary>
        /// Takes ownership of ownedMemory
        /// </summary>
        public static HashKey FromOwnedMemory(OwnedMemory ownedMemory) =>
            new (ownedMemory);

        public void Dispose()
        {
            Hash.Dispose();
        }
    }

    public readonly struct OwnedMemory : IDisposable
    {
        private static readonly ArrayPool<byte> MEMORY_POOL = ArrayPool<byte>.Shared!;

        private readonly byte[] memory;
        private readonly int length;

        public Span<byte> Memory => memory.AsMemory().Slice(0, length).Span;

        private OwnedMemory(byte[] memory, int length)
        {
            this.memory = memory;
            this.length = length;
        }

        public static OwnedMemory FromPool(int length)
        {
            byte[] memory = MEMORY_POOL.Rent(length).EnsureNotNull("Cannot rent memory");
            return new OwnedMemory(memory, length);
        }

        public static OwnedMemory FromString(string value)
        {
            int maxByteCount = Encoding.UTF8.GetMaxByteCount(value.Length);
            using var ownedMemory = FromPool(maxByteCount);
            int byteCount = Encoding.UTF8.GetBytes(value, 0, value.Length, ownedMemory.memory, 0);

            var output = FromPool(byteCount);
            Buffer.BlockCopy(ownedMemory.memory, 0, output.memory, 0, byteCount);

            return output;
        }

        public void Dispose()
        {
            MEMORY_POOL.Return(memory);
        }
    }
}
