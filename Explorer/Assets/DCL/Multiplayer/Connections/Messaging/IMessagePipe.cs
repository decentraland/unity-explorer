using Google.Protobuf;
using System;
using System.Collections.Generic;

namespace DCL.Multiplayer.Connections.Messaging
{
    public interface IMessagePipe
    {
        void Send<T>(T message, IReadOnlyCollection<string> recipientSids) where T: IMessage;

        void Subscribe<T>(Action<Message<T>> onMessageReceived) where T : class, IMessage, new();

        class Null : IMessagePipe
        {
            public static readonly Null INSTANCE = new ();

            public void Send<T>(T message, IReadOnlyCollection<string> recipientSids) where T: IMessage
            {
                //ignore
            }

            public void Subscribe<T>(Action<Message<T>> onMessageReceived) where T : class, IMessage, new()
            {
                //ignore
            }
        }
    }
}
