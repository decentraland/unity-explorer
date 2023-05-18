using System.Collections.Generic;

namespace CRDT.Protocol
{
    public interface ICRDTProtocolPoolsProvider
    {
        internal Dictionary<CRDTEntity, CRDTProtocol.EntityComponentData> GetCrdtLWWComponentsInner();

        internal void ReleaseCrdtLWWComponentsInner(Dictionary<CRDTEntity, CRDTProtocol.EntityComponentData> dictionary);

        internal Dictionary<int, Dictionary<CRDTEntity, CRDTProtocol.EntityComponentData>> GetCrdtLWWComponentsOuter();

        internal void ReleaseCrdtLWWComponentsOuter(Dictionary<int, Dictionary<CRDTEntity, CRDTProtocol.EntityComponentData>> dictionary);
    }
}
