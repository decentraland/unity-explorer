using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using Google.Protobuf;
using System;
using System.Threading;

namespace DCL.Multiplayer.Connections.Pulse.ENet
{
    public sealed class ENetTransport : ITransport
    {
        private readonly ENetTransportOptions options;
        private readonly MessagePipe messagePipe;

        private Peer? serverPeer;
        private Host client;

        public ENetTransport(
            ENetTransportOptions options,
            MessagePipe messagePipe
            )
        {
            this.options = options;
            this.messagePipe = messagePipe;
        }

        public UniTask ConnectAsync(Uri uri, CancellationToken ct)
        {
            if (!Library.Initialize())
                throw new InvalidOperationException("ENet library failed to initialize.");

            ReportHub.Verbose(ReportCategory.MULTIPLAYER, $"ENet initialized (version {Library.version}).");

            client = new Host();
            Address address = new Address();
            address.SetHost(uri.Host);
            address.Port = (ushort) uri.Port;
            client.Create();

            Peer peer = client.Connect(address, channelLimit: ENetChannel.COUNT);

            ReportHub.Verbose(ReportCategory.MULTIPLAYER, $"ENet connected (url {uri}).");

            return UniTask.CompletedTask;
        }

        public UniTask DisconnectAsync(CancellationToken ct)
        {
            serverPeer = default(Peer);
            client.Dispose();
            Library.Deinitialize();
            ReportHub.Verbose(ReportCategory.MULTIPLAYER, "ENet disconnected.");
            return UniTask.CompletedTask;
        }

        public UniTask ListenForIncomingDataAsync(CancellationToken ct)
        {
            ReportHub.Verbose(ReportCategory.MULTIPLAYER,
                $"ENet host listening on 0.0.0.0:{options.Port} (maxPeers={options.MaxPeers}).");

            // ENet must be driven on a single dedicated thread
            return UniTask.RunOnThreadPool(() =>
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
                }

                // Ensure any final outgoing packets are sent, such as disconnected notifications or any last moment data
                client.Flush();
                client.Dispose();
            }, configureAwait: false, cancellationToken: ct);
        }

        private void ReceiveIncomingMessage(ref Event netEvent)
        {
            var peerId = new PeerId(netEvent.Peer.ID);

            switch (netEvent.Type)
            {
                case EventType.Connect:
                    serverPeer = netEvent.Peer;

                    ReportHub.Verbose(ReportCategory.MULTIPLAYER,
                        $"Peer connected: {netEvent.Peer.IP}:{netEvent.Peer.Port} (id={netEvent.Peer.ID}).");

                    break;

                case EventType.Disconnect:
                    serverPeer = default(Peer);

                    ReportHub.Verbose(ReportCategory.MULTIPLAYER,
                        $"Peer disconnected: id={netEvent.Peer.ID} data={netEvent.Data}.");

                    break;

                case EventType.Timeout:
                    serverPeer = default(Peer);

                    ReportHub.Verbose(ReportCategory.MULTIPLAYER,
                        $"Peer timed out: id={netEvent.Peer.ID}.");

                    break;

                case EventType.Receive:
                {
                    using Packet _ = netEvent.Packet;

                    unsafe
                    {
                        // Parse packet
                        // ENet is not thread-safe, so this callback is always invoked on the main thread

                        messagePipe.OnDataReceived(new MessagePacket<Packet>(
                            netEvent.Packet,
                            new ReadOnlySpan<byte>((void*)netEvent.Packet.NativeData, netEvent.Packet.Length),
                            peerId));

                        break;
                    }
                }
            }
        }

        /// <summary>
        ///     ENet is not thread-safe so we are obliged to write from the same thread we read
        /// </summary>
        private void SendOutgoingMessages()
        {
            while (messagePipe.TryReadOutgoingMessage(out MessagePipe.OutgoingMessage msg))
            {
                ENetChannel channel = msg.PacketMode switch
                                      {
                                          ITransport.PacketMode.RELIABLE => ENetChannel.RELIABLE,
                                          ITransport.PacketMode.UNRELIABLE_SEQUENCED => ENetChannel.UNRELIABLE_SEQUENCED,
                                          _ => ENetChannel.UNRELIABLE_UNSEQUENCED,
                                      };

                if (serverPeer != null)
                    SendToPeer(serverPeer.Value, channel, msg.Message);
            }
        }

        private static void SendToPeer(Peer peer, ENetChannel channel, IMessage message)
        {
            // all messages are presumably very compact so allocate on stack
            Span<byte> data = stackalloc byte[message.CalculateSize()];

            message.WriteTo(data);

            var packet = default(Packet);
            packet.Create(data, channel.PacketMode);
            peer.Send(channel.ChannelId, ref packet);
        }
    }
}
