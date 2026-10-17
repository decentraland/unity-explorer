using CRDT.Memory;
using CRDT.Protocol;
using System;
using System.Text;

namespace CRDT.CRDTTests.Protocol
{
    /// <summary>
    ///     JSON Serializable Message without any optimizations
    /// </summary>
    [Serializable]
    internal class CRDTTestMessage
    {
        public CRDTMessageType type;
        public int entityId;
        public int componentId;
        public int timestamp;
        public string data;

        public CRDTMessage ToCRDTMessage(ICRDTMemoryAllocator crdtPooledMemoryAllocator) =>
            new (type, new CRDTEntity(entityId), componentId, timestamp, data != null ? crdtPooledMemoryAllocator.GetMemoryBuffer(Encoding.UTF8.GetBytes(data)) : EmptyMemoryOwner<byte>.EMPTY);
    }
}
