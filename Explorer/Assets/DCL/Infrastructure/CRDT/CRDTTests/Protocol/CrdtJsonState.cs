using System;
using System.Collections.Generic;

namespace CRDT.CRDTTests.Protocol
{
    [Serializable]
    internal class CrdtJsonState
    {
        public List<CrdtTestEntityComponentData> components;
        public List<CrdtTestEntity> deletedEntities;
    }
}
