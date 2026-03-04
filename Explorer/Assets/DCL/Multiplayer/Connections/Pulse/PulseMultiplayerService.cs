using Cysharp.Threading.Tasks;
using DCL.Web3.Chains;
using DCL.Web3.Identities;
using Decentraland.Pulse;
using Google.Protobuf;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Multiplayer.Connections.Pulse
{
    public partial class PulseMultiplayerService
    {
        private readonly ITransport transport;
        private readonly MessagePipe pipe;
        private readonly IWeb3IdentityCache identityCache;
        private readonly Dictionary<ServerMessage.MessageOneofCase, ISubscriber> subscribers = new ();
        private readonly Dictionary<string, string> authChainBuffer = new ();

        public PulseMultiplayerService(
            ITransport transport,
            MessagePipe pipe,
            IWeb3IdentityCache identityCache)
        {
            this.transport = transport;
            this.pipe = pipe;
            this.identityCache = identityCache;
        }

        public async UniTask ConnectAsync(string address, int port, CancellationToken ct)
        {
            await transport.ConnectAsync(address, port, ct);
            transport.ListenForIncomingDataAsync(ct).Forget();
            RouteIncomingMessagesAsync(ct).Forget();

            pipe.Send(new MessagePipe.OutgoingMessage(new ClientMessage
            {
                Handshake = new HandshakeRequest
                {
                    AuthChain = ByteString.CopyFromUtf8(BuildAuthChain()),
                },
            }, ITransport.PacketMode.RELIABLE));

            await foreach (HandshakeResponse response in SubscribeAsync<HandshakeResponse>(ServerMessage.MessageOneofCase.Handshake, ct))
            {
                if (!response.Success)
                    throw new PulseException(response.HasError ? response.Error : "Handshake failed");
                // Wait for handshake once
                break;
            }
        }

        public IUniTaskAsyncEnumerable<T> SubscribeAsync<T>(ServerMessage.MessageOneofCase type, CancellationToken ct)
            where T: class
        {
            var subscriber = new GenericSubscriber<T>(type);

            subscribers.Add(type, subscriber);

            return subscriber.Channel.ReadAllAsync(ct);
        }

        private async UniTaskVoid RouteIncomingMessagesAsync(CancellationToken ct)
        {
            await foreach (MessagePipe.IncomingMessage message in pipe.ReadIncomingMessagesAsync(ct))
            {
                if (!subscribers.TryGetValue(message.Message.MessageCase, out ISubscriber? subscriber)) continue;
                subscriber.TryNotify(message.Message);
            }
        }

        private string BuildAuthChain()
        {
            authChainBuffer.Clear();

            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            using AuthChain authChain = identityCache.EnsuredIdentity().Sign($"connect:/:{timestamp}:{{}}");
            var authChainIndex = 0;

            foreach (AuthLink link in authChain)
            {
                authChainBuffer[$"x-identity-auth-chain-{authChainIndex}"] = link.ToJson();
                authChainIndex++;
            }

            authChainBuffer["x-identity-timestamp"] = timestamp.ToString();
            authChainBuffer["x-identity-metadata"] = "{}";

            return JsonConvert.SerializeObject(authChainBuffer);
        }
    }
}
