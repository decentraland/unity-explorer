using System;

namespace CRDT.CRDTTests.Protocol
{
    [Serializable]
    internal class CrdtTestEntity
    {
        public int entityNumber;
        public int entityVersion;
    }
}
