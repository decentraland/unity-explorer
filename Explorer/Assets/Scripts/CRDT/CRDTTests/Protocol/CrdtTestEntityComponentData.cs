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

        internal ReadOnlyMemory<byte> GetBytes() =>
            Encoding.UTF8.GetBytes(data);

        internal CRDTProtocol.EntityComponentData ToEntityComponentData() =>
            new (timestamp, GetBytes());
    }
}
