using Decentraland.Pulse;
using Google.Protobuf;

namespace DCL.Multiplayer.Connections.Pulse
{
    public partial class PulseMultiplayerService
    {
        private class GenericSubscriber<T> : ISubscriber where T: class, IMessage
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

                var payload = message.GetUnderlyingData() as T;

                return payload != null && Channel.TryWrite(payload);
            }
        }
    }
}
