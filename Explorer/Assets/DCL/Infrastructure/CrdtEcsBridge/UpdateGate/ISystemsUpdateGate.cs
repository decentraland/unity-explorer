using Google.Protobuf;
using System;

namespace CrdtEcsBridge.UpdateGate
{
    public interface ISystemsUpdateGate : IDisposable
    {
        public void Open<T>() where T: IMessage;

        bool IsOpen<T>() where T: IMessage;
    }
}
