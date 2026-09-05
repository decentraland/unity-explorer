#nullable enable

using LiveKit.Internal.FFIClients.Pools.Memory;
using System;

namespace SocketIOClient.Transport.WebSockets
{
    public class WebSocketReceiveResult : IDisposable
    {
        public WebSocketReceiveResult(MemoryWrap memory, int count, bool endOfMessage, TransportMessageType messageType)
        {
            Count = count;
            EndOfMessage = endOfMessage;
            MessageType = messageType;
            Memory = memory;
        }

        public int Count { get; }
        public bool EndOfMessage { get; }
        public TransportMessageType MessageType { get; }
        public byte[] Buffer => Memory.DangerousBuffer();

        public MemoryWrap Memory { get; }

        public string AsText()
        {
            if (MessageType is not TransportMessageType.Text)
                throw new NotSupportedException(
                    $"Expected Text, {MessageType} messages are not supported to converting to text"
                );

            return System.Text.Encoding.UTF8.GetString(Buffer!, 0, Count);
        }

        public void Dispose()
        {
            Memory.Dispose();
        }

        ~WebSocketReceiveResult()
        {
            Dispose();
        }
    }
}
