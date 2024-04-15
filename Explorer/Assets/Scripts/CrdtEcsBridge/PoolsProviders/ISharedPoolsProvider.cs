using CRDT.Protocol.Factory;

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

        void ReleaseSerializationCrdtMessagesPool(ProcessedCRDTMessage[] messages);

        void ReleaseSerializedStateBytesPool(byte[] bytes);
    }
}
