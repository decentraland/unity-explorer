using Google.Protobuf;
using System;
using UnityEngine;

namespace DCL.Multiplayer.Connections.Messaging.Pipe
{
    public class LogMessagePipe : IMessagePipe
    {
        private readonly IMessagePipe origin;
        private readonly Action<string> log;

        public LogMessagePipe(IMessagePipe origin) : this(origin, Debug.Log)
        {
        }

        public LogMessagePipe(IMessagePipe origin, Action<string> log)
        {
            this.origin = origin;
            this.log = log;
        }

        public MessageWrap<T> NewMessage<T>() where T: class, IMessage, new()
        {
            log($"LogMessagePipe: NewMessage of type {typeof(T).FullName} requested");
            return origin.NewMessage<T>();
        }

        public void Subscribe<T>(Action<ReceivedMessage<T>> onMessageReceived) where T: class, IMessage, new()
        {
            log($"LogMessagePipe: Subscribing to messages of type {typeof(T).FullName}");
            origin.Subscribe(onMessageReceived);
        }
    }
}
