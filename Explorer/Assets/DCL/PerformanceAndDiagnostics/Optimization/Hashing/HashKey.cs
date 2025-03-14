using DCL.Utilities.Extensions;
using ECS.StreamableLoading.Cache.Disk;
using System;
using System.Buffers;
using System.Text;

namespace DCL.Optimization.Hashing
{
    public readonly struct HashKey : IDisposable, IEquatable<HashKey>
    {
        public readonly OwnedMemory Hash;

        private HashKey(OwnedMemory hash)
        {
            Hash = hash;
        }

        public static HashKey FromString(string key)
        {
            using var keyMemory = OwnedMemory.FromString(key);
            var computedHash = SHA256Hashing.ComputeHash(keyMemory.Memory.AsSpan());
            return new HashKey(computedHash);
        }

        public HashKey Copy()
        {
            using var copy = OwnedMemory.FromPool(Hash.Memory!.Length);
            Hash.Memory.CopyTo(copy.Memory, 0);
            return new HashKey(copy);
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

        public bool Equals(HashKey other)
        {
            if (Hash.Memory.Length != other.Hash.Memory.Length)
                return false;

            return Hash.Memory.AsSpan().SequenceEqual(other.Hash.Memory.AsSpan());
        }

        public override bool Equals(object obj) =>
            obj is HashKey other && Equals(other);

        public override int GetHashCode()
        {
            HashCode hashCode = new HashCode();
            foreach (byte b in Hash.Memory!) hashCode.Add(b);
            return hashCode.ToHashCode();
        }

        public override string ToString() =>
            HashNamings.HashNameFrom(this, "debug");
    }

    public readonly struct OwnedMemory : IDisposable
    {
        private static readonly ArrayPool<byte> MEMORY_POOL = ArrayPool<byte>.Shared!;

        public readonly byte[] Memory;

        private OwnedMemory(byte[] memory)
        {
            this.Memory = memory;
        }

        public static OwnedMemory FromPool(int length)
        {
            byte[] memory = MEMORY_POOL.Rent(length).EnsureNotNull("Cannot rent memory");
            return new OwnedMemory(memory);
        }

        public static OwnedMemory FromString(string value)
        {
            int maxByteCount = Encoding.UTF8.GetMaxByteCount(value.Length);
            using var ownedMemory = FromPool(maxByteCount);
            int byteCount = Encoding.UTF8.GetBytes(value, 0, value.Length, ownedMemory.Memory, 0);

            var output = FromPool(byteCount);
            Buffer.BlockCopy(ownedMemory.Memory, 0, output.Memory, 0, byteCount);

            return output;
        }

        public void Dispose()
        {
            MEMORY_POOL.Return(Memory);
        }
    }
}
