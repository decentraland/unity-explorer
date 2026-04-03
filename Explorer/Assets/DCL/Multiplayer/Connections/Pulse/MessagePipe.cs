using Cysharp.Threading.Tasks;
using Pulse.Transport;
using System.Threading;

namespace DCL.Multiplayer.Connections.Pulse
{
    /// <summary>
    /// Receives raw transport packets from the transport thread, parses them off that thread,
    /// and dispatches the resulting message to any handler.
    /// </summary>
    public sealed class MessagePipe
    {
        private readonly SimplePipeChannel<MessagePipeEvent> eventChannel = new ();
        private readonly SimplePipeChannel<OutgoingMessage> outgoingChannel = new ();

        public IUniTaskAsyncEnumerable<MessagePipeEvent> ReadEventsAsync(CancellationToken ct) =>
            eventChannel.ReadAllAsync(ct);

        public bool TryReadOutgoingMessage(out OutgoingMessage message) =>
            outgoingChannel.TryRead(out message);

        public void Send(OutgoingMessage message) =>
            outgoingChannel.TryWrite(message);

        public void OnDisconnected(DisconnectReason reason) =>
            eventChannel.TryWrite(MessagePipeEvent.FromDisconnectEvent(reason));

        /// <summary>
        ///     Called on the Transport thread for every received packet.
        ///     Must be fast and must not throw — any exception here stalls the Transport loop.
        /// </summary>
        public void OnDataReceived(MessagePacket packet)
        {
            if (IncomingMessage.TryCreate(packet.FromPeer, packet.Data, out IncomingMessage incomingMessage))
                eventChannel.TryWrite(MessagePipeEvent.FromMessage(incomingMessage));
        }
    }
}
