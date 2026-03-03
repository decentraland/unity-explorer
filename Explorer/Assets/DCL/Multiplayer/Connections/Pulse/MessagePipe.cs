using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using Decentraland.Pulse;
using System;
using System.Threading;

namespace DCL.Multiplayer.Connections.Pulse
{
    /// <summary>
    /// Receives raw transport packets from the transport thread, parses them off that thread,
    /// and dispatches the resulting message to any handler.
    /// </summary>
    public sealed class MessagePipe
    {
        private readonly SimplePipeChannel<IncomingMessage> incomingChannel = new ();
        private readonly SimplePipeChannel<OutgoingMessage> outgoingChannel = new ();

        public IUniTaskAsyncEnumerable<IncomingMessage> ReadIncomingMessagesAsync(CancellationToken ct) =>
            incomingChannel.ReadAllAsync(ct);

        public IUniTaskAsyncEnumerable<OutgoingMessage> ReadOutgoingMessagesAsync(CancellationToken ct) =>
            outgoingChannel.ReadAllAsync(ct);

        public bool TryReadOutgoingMessage(out OutgoingMessage message) =>
            outgoingChannel.TryRead(out message);

        public void Send(OutgoingMessage message) =>
            outgoingChannel.TryWrite(message);

        /// <summary>
        ///     Called on the Transport thread for every received packet.
        ///     Must be fast and must not throw — any exception here stalls the Transport loop.
        /// </summary>
        public void OnDataReceived<TTransportPacket>(MessagePacket<TTransportPacket> packet)
            where TTransportPacket: IDisposable
        {
            ServerMessage? message = null;

            try { message = ServerMessage.Parser.ParseFrom(packet.Data); }
            catch (Exception e) { ReportHub.LogWarning(ReportCategory.MULTIPLAYER, $"Pulse failed to parse packet: {e}"); }
            finally { packet.Dispose(); }

            if (message is null)
                return;

            incomingChannel.TryWrite(new IncomingMessage(packet.FromPeer, message));
        }

        public readonly struct IncomingMessage
        {
            public PeerId From { get; }
            public ServerMessage Message { get; }

            public IncomingMessage(PeerId from, ServerMessage message)
            {
                From = from;
                Message = message;
            }
        }

        public readonly struct OutgoingMessage
        {
            public ClientMessage Message { get; }
            public ITransport.PacketMode PacketMode { get; }

            public OutgoingMessage(ClientMessage message, ITransport.PacketMode packetMode)
            {
                Message = message;
                PacketMode = packetMode;
            }
        }
    }
}
