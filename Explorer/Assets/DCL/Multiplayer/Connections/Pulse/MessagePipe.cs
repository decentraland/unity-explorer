using Cysharp.Threading.Tasks;
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
        private readonly SimplePipeChannel<ITransport.DisconnectReason> disconnectChannel = new ();

        public IUniTaskAsyncEnumerable<IncomingMessage> ReadIncomingMessagesAsync(CancellationToken ct) =>
            incomingChannel.ReadAllAsync(ct);

        public IUniTaskAsyncEnumerable<OutgoingMessage> ReadOutgoingMessagesAsync(CancellationToken ct) =>
            outgoingChannel.ReadAllAsync(ct);

        public bool TryReadOutgoingMessage(out OutgoingMessage message) =>
            outgoingChannel.TryRead(out message);

        public void Send(OutgoingMessage message) =>
            outgoingChannel.TryWrite(message);

        public void OnDisconnected(ITransport.DisconnectReason reason) =>
            disconnectChannel.TryWrite(reason);

        public IUniTaskAsyncEnumerable<ITransport.DisconnectReason> ReadDisconnectsAsync(CancellationToken ct) =>
            disconnectChannel.ReadAllAsync(ct);

        /// <summary>
        ///     Called on the Transport thread for every received packet.
        ///     Must be fast and must not throw — any exception here stalls the Transport loop.
        /// </summary>
        public void OnDataReceived(MessagePacket packet)
        {
            if (IncomingMessage.TryCreate(packet.FromPeer, packet.Data, out IncomingMessage incomingMessage))
                incomingChannel.TryWrite(incomingMessage);
        }
    }
}
