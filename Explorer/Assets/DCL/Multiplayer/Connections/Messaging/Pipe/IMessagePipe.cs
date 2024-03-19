using Decentraland.Kernel.Comms.Rfc4;
using Google.Protobuf;
using System;

namespace DCL.Multiplayer.Connections.Messaging.Pipe
{
    public interface IMessagePipe
    {
        MessageWrap<T> NewMessage<T>() where T: class, IMessage, new();

        void Subscribe<T>(Packet.MessageOneofCase ofCase, Action<ReceivedMessage<T>> onMessageReceived) where T: class, IMessage, new();

        class Null : IMessagePipe
        {
            public static readonly Null INSTANCE = new ();

            public MessageWrap<T> NewMessage<T>() where T: class, IMessage, new() =>
                throw new Exception("Null implementation");

            public void Subscribe<T>(Packet.MessageOneofCase ofCase, Action<ReceivedMessage<T>> onMessageReceived) where T: class, IMessage, new()
            {
                //ignore
            }
        }
    }

    public static class MessagePipeExtensions
    {
        public static LogMessagePipe WithLog(this IMessagePipe messagePipe, string fromPipe) =>
            new (messagePipe, fromPipe);
    }
}
