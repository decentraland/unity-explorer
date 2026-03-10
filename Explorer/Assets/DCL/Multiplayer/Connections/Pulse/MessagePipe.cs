using CrdtEcsBridge.Components;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using Decentraland.Pulse;
using Google.Protobuf;
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
        public void OnDataReceived(MessagePacket packet)
        {
            ServerMessage? message = null;

            if (message is null)
                return;

            if (IncomingMessage.TryCreate(packet.FromPeer, packet.Data, out IncomingMessage incomingMessage))
                incomingChannel.TryWrite(incomingMessage);
        }

        /// <summary>
        ///     Incoming message should be disposed of as soon as it's not needed on the consumer side to prevent leaks,
        ///     The underlying proto message should be never stored
        /// </summary>
        public readonly struct IncomingMessage : IDisposable
        {
            private static readonly ServerMessagePool POOL = new ();

            public PeerId From { get; }
            public ServerMessage Message { get; }

            private IncomingMessage(PeerId from, ServerMessage message)
            {
                From = from;
                Message = message;
            }

            public static bool TryCreate(PeerId from, ReadOnlySpan<byte> data, out IncomingMessage message)
            {
                // Extract message case without parsing so we can get the message from the pool and merge it with new data
                // Extract the field number from the first tag byte.
                // The field numbers (1–4) map directly to ServerMessage.MessageOneofCase values.
                ServerMessage.MessageOneofCase messageCase = data.Length > 0
                    ? (ServerMessage.MessageOneofCase)(data[0] >> 3)
                    : ServerMessage.MessageOneofCase.None;

                message = default(IncomingMessage);

                if (messageCase == ServerMessage.MessageOneofCase.None)
                    return false;

                try
                {
                    ServerMessage packet = POOL.Get(messageCase);
                    packet.MergeFrom(data);
                    message = new IncomingMessage(from, packet);
                    return true;
                }
                catch (Exception e)
                {
                    ReportHub.LogWarning(ReportCategory.MULTIPLAYER, $"Pulse failed to parse packet: {e}");
                    return false;
                }
            }

            public void Dispose()
            {
                POOL.Release(Message);
            }
        }

        public readonly struct OutgoingMessage : IDisposable
        {
            private static readonly ClientMessagePool POOL = new ();

            public readonly ClientMessage Message;
            public readonly ITransport.PacketMode PacketMode;

            private OutgoingMessage(ClientMessage message, ITransport.PacketMode packetMode)
            {
                Message = message;
                PacketMode = packetMode;
            }

            public void Dispose()
            {
                POOL.Release(Message);
            }

            public static OutgoingMessage Create(ITransport.PacketMode packetMode, ClientMessage.MessageOneofCase kind) =>
                new (POOL.Get(kind), packetMode);
        }
    }
}
