using Cysharp.Threading.Tasks;
using Decentraland.Pulse;
using ENet;
using Google.Protobuf;
using Pulse.Transport;
using System;
using System.Threading;
using System.Threading.Tasks;
using Utility;
using Utility.Multithreading;

namespace DCL.Multiplayer.Connections.Pulse.ENet
{
    public sealed class ENetTransport : ITransport
    {
        private static volatile bool isLibInitialized;

        private readonly ENetTransportOptions options;
        private readonly MessagePipe messagePipe;
        private readonly byte[] receiveBuffer;
        private readonly byte[] sendBuffer;

        private Peer? serverPeer;
        private Host? host;
        private CancellationTokenSource? lifeCycleCts;

        private bool listenLoopIsActive;

        public long BytesSent { get; private set; }

        public long BytesReceived { get; private set; }

        public long PacketsSent { get; private set; }

        public long PacketsReceived { get; private set; }

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

        public void Dispose()
        {
            // Wait synchronously, it will lead to a main thread "freeze" but we don't care - it's called on application exit only
            // so it will wait as it should
            DisconnectAsync(DisconnectReason.GRACEFUL, true).Wait();

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

            host = new Host();

            var address = new Address();
            address.SetHost(ip);
            address.Port = (ushort)port;

            host.Create(peerLimit: 1, channelLimit: ENetChannel.COUNT);
            serverPeer = host.Connect(address, channelLimit: ENetChannel.COUNT);

            lifeCycleCts = lifeCycleCts.SafeRestartLinked(ct);
            ListenForIncomingDataAsync(host, lifeCycleCts.Token).Forget();

            try
            {
                return UniTask.WaitUntil(() => State == ITransport.TransportState.CONNECTED, cancellationToken: ct)
                              .Timeout(TimeSpan.FromMilliseconds(options.ConnectTimeoutMs));
            }
            catch (TimeoutException)
            {
                lifeCycleCts.SafeCancelAndDispose();

                // As there is no direct way to tell Connection Timeout to ENet
                // at this point it might be [already] connected or not, simply force it to disconnect
                serverPeer.Value.DisconnectNow((uint)DisconnectReason.NONE);
                host.Dispose();

                serverPeer = null;
                host = null;

                throw;
            }
        }

        public UniTask DisconnectAsync(DisconnectReason reason) =>
            DisconnectAsync(reason, false).AsUniTask();

        /// <param name="spinThread">If true: Wait on the same thread; if false - async Yield</param>
        private async Task DisconnectAsync(DisconnectReason reason, bool spinThread)
        {
            // Finish the ListenForIncomingDataAsync loop
            lifeCycleCts.SafeCancelAndDispose();

            // Wait for the loop to finish in order to prevent race conditions to ENet
            if (spinThread)
            {
                while (Volatile.Read(ref listenLoopIsActive))
                    Thread.Sleep(10);
            }
            else
            {
                while (Volatile.Read(ref listenLoopIsActive))
                    await Task.Yield();
            }

            serverPeer?.Disconnect((uint)reason);
            FinalizeHost();
        }

        /// <summary>
        ///     Makes the current connection unusable
        /// </summary>
        private void FinalizeHost()
        {
            serverPeer = null;

            // Ensure any final outgoing packets are sent, such as any last moment data and a disconnection event
            host?.Flush();
            host?.Dispose();
            host = null;
        }

        public void Send(IMessage message, PacketMode mode)
        {
            ENetChannel channel = ToENetChannel(mode);

            if (serverPeer != null)
                SendToPeer(serverPeer.Value, channel, message);
        }

        /// <summary>
        ///     The listener loop must be gracefully finalized before other ENet manipulations to prevent race conditions
        /// </summary>
        /// <param name="servingHost">The currently serving host, the class field might be changed on disconnection/reconnection</param>
        private UniTask ListenForIncomingDataAsync(Host servingHost, CancellationToken ct)
        {
            Volatile.Write(ref listenLoopIsActive, true);

            // ENet must be driven on a single dedicated thread
            return DCLTask.RunOnThreadPool(async () =>
            {
                while (!ct.IsCancellationRequested)
                {
                    // Service does socket I/O + returns one event. Short timeout so we never block outgoing flushes.
                    if (servingHost.Service(options.ServiceTimeoutMs, out Event netEvent) > 0)
                        ReceiveIncomingMessage(in netEvent);

                    // ReceiveIncomingMessage can fire the cancellation token
                    if (ct.IsCancellationRequested)
                        break;

                    // Service only returns one event per call. If multiple packets arrived in that I/O pass,
                    // the rest are queued internally. CheckEvents drains them without redundant socket I/O.
                    while (servingHost.CheckEvents(out netEvent) > 0)
                        ReceiveIncomingMessage(in netEvent);

                    SendOutgoingMessages();

                    await Task.Yield();
                }

                Volatile.Write(ref listenLoopIsActive, false);
            }, configureAwait: false, cancellationToken: ct);
        }

        private void ReceiveIncomingMessage(in Event netEvent)
        {
            var peerId = new PeerId(netEvent.Peer.ID);

            switch (netEvent.Type)
            {
                case EventType.Connect:
                    serverPeer = netEvent.Peer;
                    break;

                case EventType.Disconnect:
                    FinalizeHost();
                    lifeCycleCts.SafeCancelAndDispose();
                    messagePipe.OnDisconnected((DisconnectReason)netEvent.Data);
                    break;

                case EventType.Timeout:
                    FinalizeHost();
                    lifeCycleCts.SafeCancelAndDispose();
                    messagePipe.OnDisconnected(DisconnectReason.NONE);
                    break;

                case EventType.Receive:
                {
                    using Packet _ = netEvent.Packet;
                    netEvent.Packet.CopyTo(receiveBuffer);
                    BytesReceived += netEvent.Packet.Length;
                    PacketsReceived++;

                    messagePipe.OnDataReceived(new MessagePacket(new ReadOnlySpan<byte>(receiveBuffer, 0, netEvent.Packet.Length), peerId));

                    break;
                }
            }
        }

        /// <summary>
        ///     ENet is not thread-safe so we are obliged to write from the same thread we read.
        ///     Consecutive <see cref="ClientMessage.MessageOneofCase.Input"/> messages are collapsed:
        ///     only the latest input in a contiguous run is sent, older ones are disposed.
        /// </summary>
        private void SendOutgoingMessages()
        {
            if (!messagePipe.TryReadOutgoingMessage(out OutgoingMessage pending))
                return;

            while (messagePipe.TryReadOutgoingMessage(out OutgoingMessage next))
            {
                if (pending.Message.MessageCase == ClientMessage.MessageOneofCase.Input
                    && next.Message.MessageCase == ClientMessage.MessageOneofCase.Input)
                {
                    pending.Dispose();
                    pending = next;
                    continue;
                }

                SendAndDispose(pending);
                pending = next;
            }

            SendAndDispose(pending);
        }

        private void SendAndDispose(OutgoingMessage msg)
        {
            using OutgoingMessage _ = msg;

            if (serverPeer != null)
                SendToPeer(serverPeer.Value, ToENetChannel(msg.PacketMode), msg.Message);
        }

        private void SendToPeer(Peer peer, ENetChannel channel, IMessage message)
        {
            int size = message.CalculateSize();
            var span = new Span<byte>(sendBuffer, 0, size);
            message.WriteTo(span);
            BytesSent += size;
            PacketsSent++;
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
