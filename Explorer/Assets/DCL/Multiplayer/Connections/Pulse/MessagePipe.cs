using Pulse.Transport;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;

namespace DCL.Multiplayer.Connections.Pulse
{
    /// <summary>
    /// Receives raw transport packets from the transport thread, parses them off that thread,
    /// and dispatches the resulting message to any handler.
    /// </summary>
    public sealed class MessagePipe
    {
        private readonly Channel<MessagePipeEvent> eventChannel = Channel.CreateUnbounded<MessagePipeEvent>();
        private readonly Channel<OutgoingMessage> outgoingChannel = Channel.CreateUnbounded<OutgoingMessage>();

        public IAsyncEnumerable<MessagePipeEvent> ReadEventsAsync(CancellationToken ct) =>
            eventChannel.Reader.ReadAllAsync(ct);

        public bool TryReadOutgoingMessage(out OutgoingMessage message) =>
            outgoingChannel.Reader.TryRead(out message);

        public void Send(OutgoingMessage message) =>
            outgoingChannel.Writer.TryWrite(message);

        public void OnDisconnected(DisconnectReason reason) =>
            eventChannel.Writer.TryWrite(MessagePipeEvent.FromDisconnectEvent(reason));

        /// <summary>
        ///     Called on the Transport thread for every received packet.
        ///     Must be fast and must not throw — any exception here stalls the Transport loop.
        /// </summary>
        public void OnDataReceived(MessagePacket packet)
        {
            if (IncomingMessage.TryCreate(packet.FromPeer, packet.Data, out IncomingMessage incomingMessage))
                eventChannel.Writer.TryWrite(MessagePipeEvent.FromMessage(incomingMessage));
        }
    }
}
