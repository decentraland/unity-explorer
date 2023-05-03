using CRDT.Protocol;
using CRDT.Protocol.Factory;
using System.Buffers;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.Pool;

namespace CrdtEcsBridge.Engine
{
    public class EngineAPIPoolsProvider : IEngineAPIPoolsProvider
    {
        // CRDT Messages should not sync with other threads but be deserialized immediately instead
        private static readonly ThreadLocal<IObjectPool<IList<CRDTMessage>>> CRDT_MESSAGES_POOL =
            new (() => new ObjectPool<IList<CRDTMessage>>(

                // just preallocate an array big enough
                createFunc: () => new List<CRDTMessage>(256),
                actionOnRelease: list => list.Clear()
            ));

        // This is accessed rarely but the memory footprint is huge so share it between different scenes
        // Must be synchronized
        private static readonly ArrayPool<ProcessedCRDTMessage> PROCESSED_CRDT_MESSAGES_POOL = ArrayPool<ProcessedCRDTMessage>.Create();

        // This is accessed rarely but the memory footprint is huge so share it between different scenes
        // Must be synchronized
        private static readonly ArrayPool<byte> SERIALIZED_STATE_BYTES_POOL = ArrayPool<byte>.Create();

        public IList<CRDTMessage> GetDeserializationMessagesPool() =>
            CRDT_MESSAGES_POOL.Value.Get();

        public void ReleaseDeserializationMessagesPool(IList<CRDTMessage> messages) =>
            CRDT_MESSAGES_POOL.Value.Release(messages);

        public ProcessedCRDTMessage[] GetSerializationCrdtMessagesPool(int size)
        {
            lock (PROCESSED_CRDT_MESSAGES_POOL) { return PROCESSED_CRDT_MESSAGES_POOL.Rent(size); }
        }

        public void ReleaseSerializationCrdtMessagesPool(ProcessedCRDTMessage[] messages)
        {
            lock (PROCESSED_CRDT_MESSAGES_POOL) { PROCESSED_CRDT_MESSAGES_POOL.Return(messages); }
        }

        public byte[] GetSerializedStateBytesPool(int size)
        {
            lock (SERIALIZED_STATE_BYTES_POOL) { return SERIALIZED_STATE_BYTES_POOL.Rent(size); }
        }

        public void ReleaseSerializedStateBytesPool(byte[] bytes)
        {
            lock (SERIALIZED_STATE_BYTES_POOL) { SERIALIZED_STATE_BYTES_POOL.Return(bytes); }
        }
    }
}
