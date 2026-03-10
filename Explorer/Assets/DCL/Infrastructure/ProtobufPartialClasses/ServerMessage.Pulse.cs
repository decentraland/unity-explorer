using Google.Protobuf;
using JetBrains.Annotations;

namespace Decentraland.Pulse
{
    public partial class ServerMessage
    {
        [CanBeNull]
        public IMessage GetUnderlyingData()
        {
            return MessageCase switch
                   {
                       MessageOneofCase.Handshake => Handshake,
                       MessageOneofCase.PlayerStateFull => PlayerStateFull,
                       MessageOneofCase.PlayerStateDelta => PlayerStateDelta,
                       MessageOneofCase.PlayerJoined => PlayerJoined,
                       _ => null,
                   };
        }
    }
}
