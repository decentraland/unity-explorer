using Cysharp.Threading.Tasks;
using ENet;
using Google.Protobuf;
using Pulse.Transport;
using System;
using System.Threading;
using Utility;

namespace DCL.Multiplayer.Connections.Pulse.ENet
{
    public sealed class ENetTransport : ITransport
    {
        private static bool isLibInitialized;

        private readonly ENetTransportOptions options;
        private readonly MessagePipe messagePipe;
        private readonly byte[] receiveBuffer;
        private readonly byte[] sendBuffer;

        private Peer? serverPeer;
        private Host? client;
        private CancellationTokenSource? lifeCycleCts;

        public ENetTransport(
            ENetTransportOptions options,
            MessagePipe messagePipe
        )
        {
            this.options = options;
            this.messagePipe = messagePipe;
            receiveBuffer = new byte[options.BufferSize];
            sendBuffer = new byte[options.BufferSize];
        }

        public ITransport.TransportState State
        {
            get
            {
                if (serverPeer == null) return ITransport.TransportState.NONE;

                switch (serverPeer.Value.State)
                {
                    case PeerState.AcknowledgingConnect:
                    case PeerState.Connecting:
                    case PeerState.ConnectionPending:
                        return ITransport.TransportState.CONNECTING;

                    case PeerState.ConnectionSucceeded:
                    case PeerState.Connected:
                        return ITransport.TransportState.CONNECTED;

                    case PeerState.Disconnected:
                        return ITransport.TransportState.DISCONNECTED;

                    case PeerState.AcknowledgingDisconnect:
                    case PeerState.DisconnectLater:
                    case PeerState.Disconnecting:
                        return ITransport.TransportState.DISCONNECTING;

                    default:
                        return ITransport.TransportState.NONE;
                }
            }
        }

        public void Dispose()
        {
            serverPeer = null;
            client?.Flush();
            client?.Dispose();
            client = null;
            lifeCycleCts.SafeCancelAndDispose();

            if (isLibInitialized)
            {
                Library.Deinitialize();
                isLibInitialized = false;
            }
        }

        public UniTask ConnectAsync(string ip, int port, CancellationToken ct)
        {
            if (!isLibInitialized)
            {
                if (!Library.Initialize())
                    throw new InvalidOperationException("ENet library failed to initialize.");
                isLibInitialized = true;
            }

            client = new Host();
            Address address = new Address();
            address.SetHost(ip);
            address.Port = (ushort)port;

            client.Create(peerLimit: 1, channelLimit: ENetChannel.COUNT);
            serverPeer = client.Connect(address, channelLimit: ENetChannel.COUNT);

            lifeCycleCts = lifeCycleCts.SafeRestartLinked(ct);
            ListenForIncomingDataAsync(lifeCycleCts.Token).Forget();

            try
            {
                return UniTask.WaitUntil(() => State == ITransport.TransportState.CONNECTED, cancellationToken: ct)
                              .Timeout(TimeSpan.FromMilliseconds(options.ConnectTimeoutMs));
            }
            catch (TimeoutException)
            {
                lifeCycleCts.SafeCancelAndDispose();
                throw;
            }
        }

        public UniTask DisconnectAsync(DisconnectReason reason, CancellationToken ct)
        {
            serverPeer?.Disconnect((uint)reason);
            return UniTask.CompletedTask;
        }

        public void Send(IMessage message, PacketMode mode)
        {
            ENetChannel channel = ToENetChannel(mode);

            if (serverPeer != null)
                SendToPeer(serverPeer.Value, channel, message);
        }

        private UniTask ListenForIncomingDataAsync(CancellationToken ct)
        {
            // ENet must be driven on a single dedicated thread
            return UniTask.RunOnThreadPool(async () =>
            {
                while (!ct.IsCancellationRequested)
                {
                    var polled = false;

                    while (!polled)
                    {
                        if (client.CheckEvents(out Event netEvent) <= 0)
                        {
                            if (client.Service(options.ServiceTimeoutMs, out netEvent) <= 0)
                                break;

                            polled = true;
                        }

                        ReceiveIncomingMessage(ref netEvent);
                    }

                    SendOutgoingMessages();

                    // TODO: yield might be a problem but we need something otherwise we crash
                    await UniTask.Yield(ct);
                }

                // Ensure any final outgoing packets are sent, such as disconnected notifications or any last moment data
                client?.Flush();
                client?.Dispose();
                client = null;
            }, configureAwait: false, cancellationToken: ct);
        }

        private void ReceiveIncomingMessage(ref Event netEvent)
        {
            var peerId = new PeerId(netEvent.Peer.ID);

            switch (netEvent.Type)
            {
                case EventType.Connect:
                    serverPeer = netEvent.Peer;
                    break;

                case EventType.Disconnect:
                    serverPeer = null;
                    messagePipe.OnDisconnected((DisconnectReason)netEvent.Data);
                    lifeCycleCts.SafeCancelAndDispose();
                    break;

                case EventType.Timeout:
                    serverPeer = null;
                    messagePipe.OnDisconnected(DisconnectReason.NONE);
                    lifeCycleCts.SafeCancelAndDispose();
                    break;

                case EventType.Receive:
                {
                    using Packet _ = netEvent.Packet;
                    netEvent.Packet.CopyTo(receiveBuffer);

                    messagePipe.OnDataReceived(new MessagePacket(new ReadOnlySpan<byte>(receiveBuffer, 0, netEvent.Packet.Length), peerId));

                    break;
                }
            }
        }

        /// <summary>
        ///     ENet is not thread-safe so we are obliged to write from the same thread we read
        /// </summary>
        private void SendOutgoingMessages()
        {
            while (messagePipe.TryReadOutgoingMessage(out OutgoingMessage msg))
            {
                ENetChannel channel = ToENetChannel(msg.PacketMode);

                using OutgoingMessage _ = msg;

                if (serverPeer != null)
                    SendToPeer(serverPeer.Value, channel, msg.Message);
            }
        }

        private void SendToPeer(Peer peer, ENetChannel channel, IMessage message)
        {
            int size = message.CalculateSize();
            var span = new Span<byte>(sendBuffer, 0, size);
            message.WriteTo(span);
            var packet = default(Packet);
            packet.Create(span, channel.PacketMode);
            peer.Send(channel.ChannelId, ref packet);
        }

        private static ENetChannel ToENetChannel(PacketMode mode)
        {
            return mode switch
                   {
                       PacketMode.RELIABLE => ENetChannel.RELIABLE,
                       PacketMode.UNRELIABLE_SEQUENCED => ENetChannel.UNRELIABLE_SEQUENCED,
                       _ => ENetChannel.UNRELIABLE_UNSEQUENCED,
                   };
        }
    }
}
