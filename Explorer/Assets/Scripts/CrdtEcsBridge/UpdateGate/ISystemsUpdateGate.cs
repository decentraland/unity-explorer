using Google.Protobuf;

namespace CrdtEcsBridge.UpdateGate
{
    public interface ISystemsUpdateGate
    {
        public void Open<T>() where T: IMessage;

        bool IsOpen<T>() where T: IMessage;
    }
}
