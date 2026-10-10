using CRDT.Memory;
using CRDT.Protocol;
using System;
using System.Text;

namespace CRDT.CRDTTests.Protocol
{
    [Serializable]
    internal class CrdtTestEntityComponentData
    {
        public int entityId;
        public int componentId;
        public int timestamp;
        public string data;

        private CRDTPooledMemoryAllocator crdtPooledMemoryAllocator;

        public CrdtTestEntityComponentData()
        {
            crdtPooledMemoryAllocator = CRDTPooledMemoryAllocator.Create();
        }

        internal ReadOnlyMemory<byte> GetBytes() =>
            Encoding.UTF8.GetBytes(data);

        internal CRDTProtocol.EntityComponentData ToEntityComponentData() =>
            new (timestamp, crdtPooledMemoryAllocator.GetMemoryBuffer(GetBytes()), CRDTMessageType.PUT_COMPONENT);
    }
}
