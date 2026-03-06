using Decentraland.Pulse;

namespace DCL.Multiplayer.Connections.Pulse
{
    public partial class PulseMultiplayerService
    {
        private class GenericSubscriber<T> : ISubscriber where T: class
        {
            private readonly ServerMessage.MessageOneofCase type;

            public SimplePipeChannel<T> Channel { get; } = new ();

            public GenericSubscriber(ServerMessage.MessageOneofCase type)
            {
                this.type = type;
            }

            public bool TryNotify(ServerMessage message)
            {
                if (message.MessageCase != type) return false;

                T? payload = TryGetPayload(message);

                return payload != null && Channel.TryWrite(payload);
            }

            private static T? TryGetPayload(ServerMessage message)
            {
                return message.MessageCase switch
                       {
                           ServerMessage.MessageOneofCase.Handshake => message.Handshake as T,
                           _ => null
                       };
            }
        }
    }
}
