using CRDT;
using Google.Protobuf;

namespace CrdtEcsBridge.ECSToCRDTWriter
{
    public interface IECSToCRDTWriter
    {
        void PutMessage<T>(CRDTEntity crdtID, int componentId, T model) where T: IMessage<T>;

        void AppendMessage<T>(CRDTEntity crdtID, int componentId, T model) where T: IMessage<T>;

        void DeleteMessage(CRDTEntity crdtID, int componentId);
    }
}
