using Cysharp.Threading.Tasks;
using DCL.Utilities.Extensions;
using DCL.Web3.Chains;
using DCL.Web3.Identities;
using Decentraland.Pulse;
using Google.Protobuf;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading;
using Utility;

namespace DCL.Multiplayer.Connections.Pulse
{
    public partial class PulseMultiplayerService
    {
        private readonly ITransport transport;
        private readonly MessagePipe pipe;
        private readonly IWeb3IdentityCache identityCache;
        private readonly Dictionary<ServerMessage.MessageOneofCase, ISubscriber> subscribers = new ();
        private readonly Dictionary<string, string> authChainBuffer = new ();
        private CancellationTokenSource? connectionLifeCycleCts;

        public PulseMultiplayerService(
            ITransport transport,
            MessagePipe pipe,
            IWeb3IdentityCache identityCache)
        {
            this.transport = transport;
            this.pipe = pipe;
            this.identityCache = identityCache;
        }

        public async UniTask ConnectAsync(CancellationToken ct)
        {
            if (transport.State is ITransport.TransportState.CONNECTED or ITransport.TransportState.CONNECTING)
                return;

            // TODO: get the address from IDecentralandUrlsSource (?)
            await transport.ConnectAsync("127.0.0.1", 7777, ct);

            connectionLifeCycleCts = connectionLifeCycleCts.SafeRestartLinked(ct);
            transport.ListenForIncomingDataAsync(connectionLifeCycleCts.Token).SuppressToResultAsync().Forget();
            RouteIncomingMessagesAsync(connectionLifeCycleCts.Token).Forget();

            var handshakePacket = MessagePipe.OutgoingMessage.Create(ITransport.PacketMode.RELIABLE, ClientMessage.MessageOneofCase.Handshake);
            handshakePacket.Message.Handshake.AuthChain = ByteString.CopyFromUtf8(BuildAuthChain());

            Send(handshakePacket);

            await foreach (HandshakeResponse response in SubscribeAsync<HandshakeResponse>(ServerMessage.MessageOneofCase.Handshake, ct))
            {
                if (!response.Success)
                {
                    await DisconnectAsync(ct);
                    throw new PulseException(response.HasError ? response.Error : "Handshake failed");
                }

                // Wait for handshake once
                break;
            }
        }

        public async UniTask DisconnectAsync(CancellationToken ct)
        {
            connectionLifeCycleCts.SafeCancelAndDispose();
            await transport.DisconnectAsync(ct);
        }

        public IUniTaskAsyncEnumerable<T> SubscribeAsync<T>(ServerMessage.MessageOneofCase type, CancellationToken ct)
            where T: class, IMessage
        {
            var subscriber = new GenericSubscriber<T>(type);

            subscribers.Add(type, subscriber);

            return subscriber.Channel.ReadAllAsync(ct);
        }

        public void Send(MessagePipe.OutgoingMessage outgoingMessage) =>
            pipe.Send(outgoingMessage);

        private async UniTaskVoid RouteIncomingMessagesAsync(CancellationToken ct)
        {
            try
            {
                await foreach (MessagePipe.IncomingMessage message in pipe.ReadIncomingMessagesAsync(ct))
                {
                    if (!subscribers.TryGetValue(message.Message.MessageCase, out ISubscriber? subscriber)) continue;
                    subscriber.TryNotify(message.Message);
                }
            }
            catch (OperationCanceledException)
            {
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
