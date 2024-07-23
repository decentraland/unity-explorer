using CRDT.Protocol.Factory;
using SceneRuntime.Apis.Modules.EngineApi.SDKObservableEvents.Events;

namespace CrdtEcsBridge.PoolsProviders
{
    /// <summary>
    ///     Provides threads-synchronized pools for heavily-loaded bulk serialization and deserialization
    ///     shared between all scene instances (threads)
    /// </summary>
    public interface ISharedPoolsProvider
    {
        ProcessedCRDTMessage[] GetSerializationCrdtMessagesPool(int size);

        PoolableByteArray GetSerializedStateBytesPool(int size);

        PoolableSDKObservableEventArray GetSerializationSDKObservableEventsPool(int size);

        void ReleaseSerializationCrdtMessagesPool(ProcessedCRDTMessage[] messages);

        void ReleaseSerializedStateBytesPool(byte[] bytes);

        void ReleaseSerializationSDKObservableEventsPool(SDKObservableEvent[] events);
    }
}
