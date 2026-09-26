using SocketIOClient.Transport;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace SocketIOClient.Messages
{
    public class DefaultTextMessage : IMessage
    {
        public MessageType Type => MessageType.DefaultTextMessage;
        public List<byte[]> OutgoingBytes { get; set; }
        public List<byte[]> IncomingBytes { get; set; }
        public int BinaryCount { get; }
        public EngineIO EIO { get; set; }
        public TransportProtocol Protocol { get; set; }

        public string Message { get; set; }

        public void Read(string msg)
        {
            Message = msg;
        }

        public string Write() =>
            throw new NotImplementedException();
    }
}
