using Decentraland.Kernel.Comms.Rfc4;
using ECS.SceneLifeCycle;
using Google.Protobuf;
using System;

namespace DCL.Multiplayer.Connections.Messaging.Pipe
{
    public interface IMessagePipe : IDisposable
    {
        enum ThreadStrict
        {
            /// <summary>
            /// Requires to receive the events on main thread.
            /// </summary>
            MAIN_THREAD_ONLY,
            /// <summary>
            /// States to receive the events on any thread that the original message comes from.
            /// </summary>
            ORIGIN_THREAD
        }

        MessageWrap<T> NewMessage<T>(string topic = "") where T: class, IMessage, new();

        void Subscribe<T>(Packet.MessageOneofCase ofCase, Action<ReceivedMessage<T>> onMessageReceived, ThreadStrict threadStrict = ThreadStrict.MAIN_THREAD_ONLY) where T: class, IMessage, new();

        class Null : IMessagePipe
        {
            private Null() {}

            public static readonly Null INSTANCE = new ();

            public MessageWrap<T> NewMessage<T>(string topic) where T: class, IMessage, new() =>
                throw new Exception("Null implementation");

            public void Subscribe<T>(Packet.MessageOneofCase _, Action<ReceivedMessage<T>> __, ThreadStrict ___) where T: class, IMessage, new() { }
            public void Dispose() { }
        }
    }

    public static class MessagePipeBuilder
    {
        public static LogMessagePipe WithLog(this IMessagePipe messagePipe, string fromPipe) =>
            new (messagePipe, fromPipe);
    }
}
