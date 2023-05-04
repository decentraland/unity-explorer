using CRDT;
using CrdtEcsBridge.Serialization;

namespace CrdtEcsBridge.ECSToCRDTWriter
{
    public interface IECSToCRDTWriter
    {
        void PutMessage<T>(CRDTEntity crdtID, int componentId, T model);

        void AppendMessage<T>(CRDTEntity crdtID, int componentId, T model);

        void DeleteMessage(CRDTEntity crdtID, int componentId);

        void RegisterSerializer(int componentId, IComponentSerializer serializer);
    }
}
