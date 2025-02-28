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
        private readonly string fromPipe;
        private readonly Dictionary<Type, string> cachedMessages = new ();

        public LogMessagePipe(IMessagePipe origin, string fromPipe)
        {
            this.origin = origin;
            this.fromPipe = fromPipe;
        }

        public MessageWrap<T> NewMessage<T>() where T: class, IMessage, new()
        {
            ReportHub.Log(ReportCategory.LIVEKIT, LogForNewMessage<T>());
            return origin.NewMessage<T>();
        }

        public void Subscribe<T>(Packet.MessageOneofCase ofCase, Action<ReceivedMessage<T>> onMessageReceived, IMessagePipe.ThreadStrict threadStrict) where T: class, IMessage, new()
        {
            ReportHub.Log(
                ReportCategory.LIVEKIT, $"From: {fromPipe} LogMessagePipe: Subscribing to messages of type {typeof(T).FullName}");

            origin.Subscribe<T>(
                ofCase,
                rm =>
                {
                    ReportHub.Log(
                        ReportCategory.LIVEKIT, $"From: {fromPipe} LogMessagePipe: Received message of type {typeof(T).FullName} with content {rm.Payload} from {rm.FromWalletId}");

                    onMessageReceived(rm);
                },
                threadStrict
            );
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

            cachedMessages[type] = message = $"From: {fromPipe} LogMessagePipe: NewMessage of type {typeof(T).FullName} requested";
            return message;
        }
    }
}
