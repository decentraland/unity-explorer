using Google.Protobuf;
using System;

namespace DCL.Multiplayer.Connections.Messaging.Pipe
{
    public class InteriorMessagePipe : IMessagePipe
    {
        private IMessagePipe messagePipe = new IMessagePipe.Null();

        //private List<>//TODO resubscribe list

        public void Assign(IMessagePipe pipe) =>
            messagePipe = pipe;

        public MessageWrap<T> NewMessage<T>() where T: class, IMessage, new() =>
            messagePipe.NewMessage<T>();

        public void Subscribe<T>(Action<ReceivedMessage<T>> onMessageReceived) where T: class, IMessage, new() =>
            messagePipe.Subscribe(onMessageReceived);
    }
}
