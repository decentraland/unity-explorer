using CRDT.Protocol.Factory;
using SceneRuntime.Apis.Modules.EngineApi.SDKObservableEvents.Events;
using System;
using System.Buffers;

namespace CrdtEcsBridge.PoolsProviders
{
    public class SharedPoolsProvider : ISharedPoolsProvider
    {
        private static readonly ArrayPool<ProcessedCRDTMessage> PROCESSED_CRDT_MESSAGES_POOL = ArrayPool<ProcessedCRDTMessage>.Create();

        private static readonly ArrayPool<byte> SERIALIZED_STATE_BYTES_POOL = ArrayPool<byte>.Create();

        private static readonly ArrayPool<SDKObservableEvent> SDK_OBSERVABLE_EVENTS_POOL = ArrayPool<SDKObservableEvent>.Create();

        private readonly Action<byte[]> bytesPoolReleaseFuncCached;
        private readonly Action<SDKObservableEvent[]> sdkObservableEventsPoolReleaseFuncCached;

        public SharedPoolsProvider()
        {
            bytesPoolReleaseFuncCached = ReleaseSerializedStateBytesPool;
            sdkObservableEventsPoolReleaseFuncCached = ReleaseSerializationSDKObservableEventsPool;
        }

        public ProcessedCRDTMessage[] GetSerializationCrdtMessagesPool(int size) =>
            PROCESSED_CRDT_MESSAGES_POOL.Rent(size);

        public void ReleaseSerializationCrdtMessagesPool(ProcessedCRDTMessage[] messages) =>
            PROCESSED_CRDT_MESSAGES_POOL.Return(messages);

        public PoolableByteArray GetSerializedStateBytesPool(int size) =>
            new (SERIALIZED_STATE_BYTES_POOL.Rent(size), size, bytesPoolReleaseFuncCached);

        public void ReleaseSerializedStateBytesPool(byte[] bytes) =>
            SERIALIZED_STATE_BYTES_POOL.Return(bytes, true);

        public PoolableSDKObservableEventArray GetSerializationSDKObservableEventsPool(int size) =>
            new (SDK_OBSERVABLE_EVENTS_POOL.Rent(size), size, sdkObservableEventsPoolReleaseFuncCached);

        public void ReleaseSerializationSDKObservableEventsPool(SDKObservableEvent[] events) =>
            SDK_OBSERVABLE_EVENTS_POOL.Return(events, true);
    }
}
