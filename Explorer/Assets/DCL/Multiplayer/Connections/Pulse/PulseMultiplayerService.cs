using Cysharp.Threading.Tasks;
using Decentraland.Pulse;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Multiplayer.Connections.Pulse
{
    public class PulseMultiplayerService
    {
        private readonly MessagePipe pipe;
        private readonly Dictionary<ServerMessage.MessageOneofCase, ISubscriber> subscribers = new ();

        public PulseMultiplayerService(
            MessagePipe pipe)
        {
            this.pipe = pipe;
        }

        public async UniTask RouteIncomingMessagesAsync(CancellationToken ct)
        {
            await foreach (MessagePipe.IncomingMessage message in pipe.ReadIncomingMessagesAsync(ct))
            {
                if (!subscribers.TryGetValue(message.Message.MessageCase, out ISubscriber? subscriber)) continue;
                subscriber.TryNotify(message.Message);
            }
        }

        public IUniTaskAsyncEnumerable<T> SubscribeAsync<T>(ServerMessage.MessageOneofCase type, CancellationToken ct)
            where T: class
        {
            var subscriber = new GenericSubscriber<T>(type);

            subscribers.Add(type, subscriber);

            return subscriber.Channel.ReadAllAsync(ct);
        }

        private interface ISubscriber
        {
            bool TryNotify(ServerMessage message);
        }

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
