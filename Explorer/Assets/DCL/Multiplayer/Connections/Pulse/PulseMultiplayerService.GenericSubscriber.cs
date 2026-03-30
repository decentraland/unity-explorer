using Decentraland.Pulse;
using Google.Protobuf;

namespace DCL.Multiplayer.Connections.Pulse
{
    public partial class PulseMultiplayerService
    {
        private class GenericSubscriber<T> : ISubscriber where T: class, IMessage
        {
            private readonly ServerMessage.MessageOneofCase type;

            public SimplePipeChannel<IncomingMessage<T>> Channel { get; } = new ();

            public GenericSubscriber(ServerMessage.MessageOneofCase type)
            {
                this.type = type;
            }

            public bool TryNotify(IncomingMessage message)
            {
                if (message.Message.MessageCase != type) return false;

                var payload = message.Message.GetUnderlyingData() as T;

                if (payload == null)
                {
                    message.Dispose();
                    return false;
                }

                // Disposal is handled automatically by AutoDisposeAsyncEnumerable when the next message is consumed
                return Channel.TryWrite(new IncomingMessage<T>(message, payload));
            }
        }
    }
}
