using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace DCL.Optimization.Hashing
{
    public static class SHA256Hashing
    {
        public const int SHA256_HASH_LENGTH = 32; //sha256 produces 32 bits
        private static readonly SHA256 SHA256 = SHA256.Create()!;

        public static OwnedMemory ComputeHash(ReadOnlySpan<byte> source)
        {
            var hashMemory = OwnedMemory.FromPool(SHA256_HASH_LENGTH);

            if (SHA256.TryComputeHash(source, hashMemory.Memory, out _) == false)
                throw new Exception("Something went wrong during hash computation");

            return hashMemory;
        }

        public static OwnedMemory ComputeHash(IEnumerable<OwnedMemory> list)
        {
            using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256)!;

            foreach (OwnedMemory ownedMemory in list)
                hasher.AppendData(ownedMemory.Memory);

            var hashMemory = OwnedMemory.FromPool(SHA256_HASH_LENGTH);

            if (hasher.TryGetHashAndReset(hashMemory.Memory, out _) == false)
                throw new Exception("Something went wrong during hash computation");

            return hashMemory;
        }
    }
}
