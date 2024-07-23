using DCL.Diagnostics;
using Decentraland.Kernel.Comms.Rfc4;
using Google.Protobuf;
using System;
using System.Collections.Generic;

namespace DCL.Multiplayer.Connections.Messaging.Pipe
{
    public class LogMessagePipe : IMessagePipe
    {
        private readonly IMessagePipe origin;
        private readonly Action<string> log;
        private readonly Dictionary<Type, string> cachedMessages = new ();

        public LogMessagePipe(IMessagePipe origin, string fromPipe) : this(
            origin,
            m => ReportHub
                .WithReport(ReportCategory.LIVEKIT)
                .Log($"From: {fromPipe} - {m}")
        ) { }

        public LogMessagePipe(IMessagePipe origin, Action<string> log)
        {
            this.origin = origin;
            this.log = log;
        }

        public MessageWrap<T> NewMessage<T>() where T: class, IMessage, new()
        {
            log(LogForNewMessage<T>());
            return origin.NewMessage<T>();
        }

        public void Subscribe<T>(Packet.MessageOneofCase ofCase, Action<ReceivedMessage<T>> onMessageReceived) where T: class, IMessage, new()
        {
            log($"LogMessagePipe: Subscribing to messages of type {typeof(T).FullName}");

            origin.Subscribe<T>(ofCase, rm =>
            {
                log($"LogMessagePipe: Received message of type {typeof(T).FullName} with content {rm.Payload} from {rm.FromWalletId}");
                onMessageReceived(rm);
            });
        }

        public void Dispose()
        {
            origin.Dispose();
        }

        private string LogForNewMessage<T>()
        {
            var type = typeof(T);

            if (cachedMessages.TryGetValue(type, out string? message))
                return message!;

            cachedMessages[type] = message = $"LogMessagePipe: NewMessage of type {typeof(T).FullName} requested";
            return message;
        }
    }
}
