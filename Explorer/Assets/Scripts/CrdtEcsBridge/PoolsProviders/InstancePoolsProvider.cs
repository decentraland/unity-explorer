using CRDT.Protocol;
using DCL.Optimization.Pools;
using DCL.Optimization.ThreadSafePool;
using System;
using System.Buffers;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace CrdtEcsBridge.PoolsProviders
{
    public class InstancePoolsProvider : IInstancePoolsProvider
    {
        /// <summary>
        ///     Reuse pools when the scene is disposed
        /// </summary>
        private static readonly ThreadSafeObjectPool<InstancePoolsProvider> POOL =
            new (() => new InstancePoolsProvider(), defaultCapacity: PoolConstants.SCENES_COUNT);

        private readonly IObjectPool<List<CRDTMessage>> crdtMessagesPool = new ListObjectPool<CRDTMessage>(listInstanceDefaultCapacity: 256);

        private readonly ArrayPool<byte> crdtRawDataPool = ArrayPool<byte>.Create(16_777_216, 8); // 16 MB, 8 buckets, if the requested array is more than 16 mb than a new instance is simply returned

        private readonly Action<byte[]> releaseFuncCached;

        // Forbid creating instances of this class outside of the pool
        private InstancePoolsProvider()
        {
            releaseFuncCached = ReleaseCrdtRawDataPool;
        }

        public void Dispose()
        {
            POOL.Release(this);
        }

        public static InstancePoolsProvider Create() =>
            POOL.Get();

        public PoolableByteArray GetCrdtRawDataPool(int size) =>
            new (crdtRawDataPool.Rent(size), size, releaseFuncCached);

        private void ReleaseCrdtRawDataPool(byte[] bytes)
        {
            crdtRawDataPool.Return(bytes);
        }

        public List<CRDTMessage> GetDeserializationMessagesPool() =>
            crdtMessagesPool.Get();

        public void ReleaseDeserializationMessagesPool(List<CRDTMessage> messages)
        {
            crdtMessagesPool.Release(messages);
        }
    }
}
