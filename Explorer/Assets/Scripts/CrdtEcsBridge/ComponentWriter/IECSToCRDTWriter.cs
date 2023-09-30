using CRDT;
using Google.Protobuf;

namespace CrdtEcsBridge.ECSToCRDTWriter
{
    public interface IECSToCRDTWriter
    {
        void PutMessage<T>(CRDTEntity crdtID, T model) where T: IMessage;

        void AppendMessage<T>(CRDTEntity crdtID, T model) where T: IMessage;

        void DeleteMessage<T>(CRDTEntity crdtID) where T: IMessage;
    }
}
