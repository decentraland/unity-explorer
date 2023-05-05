using CRDT.Protocol;
using CRDT.Protocol.Factory;
using System.Collections.Generic;

namespace CrdtEcsBridge.Engine
{
    /// <summary>
    /// Provides threads-synchronized pools for heavily-loaded bulk serialization and deserialization
    /// </summary>
    public interface IEngineAPIPoolsProvider
    {
        IList<CRDTMessage> GetDeserializationMessagesPool();

        void ReleaseDeserializationMessagesPool(IList<CRDTMessage> messages);

        ProcessedCRDTMessage[] GetSerializationCrdtMessagesPool(int size);

        byte[] GetSerializedStateBytesPool(int size);

        void ReleaseSerializationCrdtMessagesPool(ProcessedCRDTMessage[] messages);

        void ReleaseSerializedStateBytesPool(byte[] bytes);
    }
}
