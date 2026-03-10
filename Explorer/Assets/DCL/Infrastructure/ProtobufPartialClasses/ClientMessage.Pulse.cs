using Google.Protobuf;
using JetBrains.Annotations;

namespace Decentraland.Pulse
{
    public partial class ClientMessage
    {
        [CanBeNull]
        public IMessage GetUnderlyingData()
        {
            return MessageCase switch
                   {
                       MessageOneofCase.Handshake => Handshake,
                       MessageOneofCase.Input => Input,
                       _ => null,
                   };
        }
    }
}
