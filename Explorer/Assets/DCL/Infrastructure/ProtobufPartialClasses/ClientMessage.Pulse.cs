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
                       MessageOneofCase.Resync => Resync,
                       MessageOneofCase.ProfileAnnouncement => ProfileAnnouncement,
                       MessageOneofCase.EmoteStart => EmoteStart,
                       MessageOneofCase.EmoteStop => EmoteStop,
                       _ => null,
                   };
        }
    }
}
