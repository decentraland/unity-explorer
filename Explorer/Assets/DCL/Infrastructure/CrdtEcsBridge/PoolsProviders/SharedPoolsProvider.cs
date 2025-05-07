using CRDT.Protocol.Factory;
using SceneRuntime.Apis.Modules.EngineApi.SDKObservableEvents.Events;
using System;
using System.Buffers;

namespace CrdtEcsBridge.PoolsProviders
{
    public class SharedPoolsProvider : ISharedPoolsProvider
    {
        // This is accessed rarely but the memory footprint is huge so share it between different scenes
        // Must be synchronized
        private static readonly ArrayPool<ProcessedCRDTMessage> PROCESSED_CRDT_MESSAGES_POOL = ArrayPool<ProcessedCRDTMessage>.Create();

        // This is accessed rarely but the memory footprint is huge so share it between different scenes
        // Must be synchronized
        private static readonly ArrayPool<byte> SERIALIZED_STATE_BYTES_POOL = ArrayPool<byte>.Create();

        private static readonly ArrayPool<SDKObservableEvent> SDK_OBSERVABLE_EVENTS_POOL = ArrayPool<SDKObservableEvent>.Create();

        private readonly Action<byte[]> bytesPoolReleaseFuncCached;
        private readonly Action<SDKObservableEvent[]> sdkObservableEventsPoolReleaseFuncCached;

        public SharedPoolsProvider()
        {
            bytesPoolReleaseFuncCached = ReleaseSerializedStateBytesPool;
            sdkObservableEventsPoolReleaseFuncCached = ReleaseSerializationSDKObservableEventsPool;
        }

        public ProcessedCRDTMessage[] GetSerializationCrdtMessagesPool(int size)
        {
            lock (PROCESSED_CRDT_MESSAGES_POOL) { return PROCESSED_CRDT_MESSAGES_POOL.Rent(size); }
        }

        public void ReleaseSerializationCrdtMessagesPool(ProcessedCRDTMessage[] messages)
        {
            lock (PROCESSED_CRDT_MESSAGES_POOL) { PROCESSED_CRDT_MESSAGES_POOL.Return(messages); }
        }

        public PoolableByteArray GetSerializedStateBytesPool(int size)
        {
            lock (SERIALIZED_STATE_BYTES_POOL) { return new PoolableByteArray(SERIALIZED_STATE_BYTES_POOL.Rent(size), size, bytesPoolReleaseFuncCached); }
        }

        public void ReleaseSerializedStateBytesPool(byte[] bytes)
        {
            lock (SERIALIZED_STATE_BYTES_POOL) { SERIALIZED_STATE_BYTES_POOL.Return(bytes, true); }
        }

        public PoolableSDKObservableEventArray GetSerializationSDKObservableEventsPool(int size)
        {
            lock (SDK_OBSERVABLE_EVENTS_POOL) { return new PoolableSDKObservableEventArray(SDK_OBSERVABLE_EVENTS_POOL.Rent(size), size, sdkObservableEventsPoolReleaseFuncCached); }
        }

        public void ReleaseSerializationSDKObservableEventsPool(SDKObservableEvent[] events)
        {
            lock (SDK_OBSERVABLE_EVENTS_POOL) { SDK_OBSERVABLE_EVENTS_POOL.Return(events, true); }
        }
    }
}
