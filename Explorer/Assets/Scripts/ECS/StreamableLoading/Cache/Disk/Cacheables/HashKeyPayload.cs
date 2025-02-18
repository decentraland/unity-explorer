using DCL.Optimization.Hashing;
using DCL.Optimization.ThreadSafePool;
using System.Buffers.Binary;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace ECS.StreamableLoading.Cache.Disk.Cacheables
{
    public class HashKeyPayload : IHashKeyPayload
    {
        private static readonly ThreadSafeObjectPool<HashKeyPayload> DISK_HASH_PAYLOAD_POOL = new (
            static () => new HashKeyPayload(),
            actionOnRelease: static p => p.Clear()
        );

        private readonly List<OwnedMemory> memoryList;

        private HashKeyPayload()
        {
            memoryList = new List<OwnedMemory>();
        }

        public static PooledObject<HashKeyPayload> NewDiskHashPayload(out HashKeyPayload keyPayload) =>
            DISK_HASH_PAYLOAD_POOL.Get(out keyPayload);

        public HashKey NewHashKey()
        {
            var memory = SHA256Hashing.ComputeHash(memoryList);
            return HashKey.FromOwnedMemory(memory);
        }

        private void Clear()
        {
            foreach (OwnedMemory ownedMemory in memoryList) ownedMemory.Dispose();
            memoryList.Clear();
        }

        public void Put(string value)
        {
            var memory = OwnedMemory.FromString(value);
            memoryList.Add(memory);
        }

        public void Put(int value)
        {
            var memory = OwnedMemory.FromPool(sizeof(int));
            BinaryPrimitives.WriteInt32LittleEndian(memory.Memory, value);
            memoryList.Add(memory);
        }

        public void Put(bool value)
        {
            var memory = OwnedMemory.FromPool(sizeof(int));
            BinaryPrimitives.WriteInt32LittleEndian(memory.Memory, value ? 1 : 0);
            memoryList.Add(memory);
        }
    }
}
