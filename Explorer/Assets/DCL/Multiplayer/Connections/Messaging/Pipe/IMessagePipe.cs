using Google.Protobuf;
using System;

namespace DCL.Multiplayer.Connections.Messaging.Pipe
{
    public interface IMessagePipe
    {
        MessageWrap<T> NewMessage<T>() where T: class, IMessage, new();

        void Subscribe<T>(Action<ReceivedMessage<T>> onMessageReceived) where T: class, IMessage, new();

        class Null : IMessagePipe
        {
            public static readonly Null INSTANCE = new ();

            public MessageWrap<T> NewMessage<T>() where T: class, IMessage, new() =>
                throw new Exception("Null implementation");

            public void Subscribe<T>(Action<ReceivedMessage<T>> onMessageReceived) where T: class, IMessage, new()
            {
                //ignore
            }
        }
    }
}
