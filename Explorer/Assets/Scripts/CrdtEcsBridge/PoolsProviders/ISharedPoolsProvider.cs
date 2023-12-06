using CRDT.Protocol.Factory;

namespace CrdtEcsBridge.Engine
{
    /// <summary>
    ///     Provides threads-synchronized pools for heavily-loaded bulk serialization and deserialization
    ///     shared between all scene instances (threads)
    /// </summary>
    public interface ISharedPoolsProvider
    {
        ProcessedCRDTMessage[] GetSerializationCrdtMessagesPool(int size);

        byte[] GetSerializedStateBytesPool(int size);

        void ReleaseSerializationCrdtMessagesPool(ProcessedCRDTMessage[] messages);

        void ReleaseSerializedStateBytesPool(byte[] bytes);
    }
}
