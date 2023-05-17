using CRDT;
using CRDT.Protocol;
using DCL.ECS7;
using System.Buffers;
using System.Collections.Generic;
using UnityEngine.Pool;
using Utility.ThreadSafePool;

namespace CrdtEcsBridge.PoolsProviders
{
    public class InstancePoolsProvider : IInstancePoolsProvider, ICRDTProtocolPoolsProvider
    {
        internal const int DESERIALIZATION_LIST_CAPACITY = 256;

        /// <summary>
        ///     It should be slightly greater or equal to the number of <see cref="ComponentID" />'s
        /// </summary>
        internal const int CRDT_LWW_COMPONENTS_OUTER_CAPACITY = 32;

        /// <summary>
        ///     It refers to the entities for each component, just cap it at some number that is unlikely to reach on average
        /// </summary>
        internal const int CRDT_LWW_COMPONENTS_INNER_CAPACITY = 512;

        /// <summary>
        ///     Reuse pools when the scene is disposed
        /// </summary>
        private static readonly ThreadSafeObjectPool<InstancePoolsProvider> POOL =
            new (() => new InstancePoolsProvider());

        private readonly IObjectPool<Dictionary<CRDTEntity, CRDTProtocol.EntityComponentData>> crdtLWWComponentsInnerPool =
            new ObjectPool<Dictionary<CRDTEntity, CRDTProtocol.EntityComponentData>>(
                createFunc: () => new Dictionary<CRDTEntity, CRDTProtocol.EntityComponentData>(CRDT_LWW_COMPONENTS_INNER_CAPACITY, CRDTEntityComparer.INSTANCE),
                actionOnRelease: dictionary => dictionary.Clear(),
                defaultCapacity: CRDT_LWW_COMPONENTS_OUTER_CAPACITY,
                maxSize: CRDT_LWW_COMPONENTS_OUTER_CAPACITY
            );

        private readonly Dictionary<int, Dictionary<CRDTEntity, CRDTProtocol.EntityComponentData>> crdtLWWComponentsOuter =
            new (CRDT_LWW_COMPONENTS_OUTER_CAPACITY);

        private readonly IObjectPool<IList<CRDTMessage>> crdtMessagesPool = new ObjectPool<IList<CRDTMessage>>(

            // just preallocate an array big enough
            createFunc: () => new List<CRDTMessage>(DESERIALIZATION_LIST_CAPACITY),
            actionOnRelease: list => list.Clear()
        );

        private readonly ArrayPool<byte> crdtRawDataPool = ArrayPool<byte>.Create(16 * 1024 * 1024, 8); // 16 MB, 8 buckets, if the requested array is more than 16 mb than a new instance is simply returned

        // Forbid creating instances of this class outside of the pool
        private InstancePoolsProvider() { }

        public void Dispose()
        {
            POOL.Release(this);
        }

        public byte[] GetCrdtRawDataPool(int size) =>
            crdtRawDataPool.Rent(size);

        public void ReleaseCrdtRawDataPool(byte[] bytes)
        {
            crdtRawDataPool.Return(bytes);
        }

        public IList<CRDTMessage> GetDeserializationMessagesPool() =>
            crdtMessagesPool.Get();

        public void ReleaseDeserializationMessagesPool(IList<CRDTMessage> messages)
        {
            crdtMessagesPool.Release(messages);
        }

        Dictionary<CRDTEntity, CRDTProtocol.EntityComponentData> ICRDTProtocolPoolsProvider.GetCrdtLWWComponentsInner() =>
            crdtLWWComponentsInnerPool.Get();

        void ICRDTProtocolPoolsProvider.ReleaseCrdtLWWComponentsInner(Dictionary<CRDTEntity, CRDTProtocol.EntityComponentData> dictionary) =>
            crdtLWWComponentsInnerPool.Release(dictionary);

        Dictionary<int, Dictionary<CRDTEntity, CRDTProtocol.EntityComponentData>> ICRDTProtocolPoolsProvider.GetCrdtLWWComponentsOuter() =>
            crdtLWWComponentsOuter;

        void ICRDTProtocolPoolsProvider.ReleaseCrdtLWWComponentsOuter(Dictionary<int, Dictionary<CRDTEntity, CRDTProtocol.EntityComponentData>> dictionary) =>
            dictionary.Clear();

        public static InstancePoolsProvider Create() =>
            POOL.Get();
    }
}
