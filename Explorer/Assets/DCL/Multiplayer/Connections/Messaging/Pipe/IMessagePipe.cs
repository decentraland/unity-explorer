using DCL.Multiplayer.Connections.Rooms.Nulls;
using Decentraland.Kernel.Comms.Rfc4;
using Google.Protobuf;
using LiveKit.Internal.FFIClients.Pools;
using LiveKit.Internal.FFIClients.Pools.Memory;
using System;

namespace DCL.Multiplayer.Connections.Messaging.Pipe
{
    public interface IMessagePipe : IDisposable
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

            public void Dispose() { }
        }

        class Fake : IMessagePipe
        {
            private readonly IMultiPool multiPool = new ThreadSafeMultiPool();
            private readonly IMemoryPool memoryPool = new ArrayMemoryPool();

            public void Dispose()
            {
                //
            }

            public MessageWrap<T> NewMessage<T>() where T: class, IMessage, new() =>
                new (NullDataPipe.INSTANCE, multiPool, memoryPool, 0);

            public void Subscribe<T>(Packet.MessageOneofCase ofCase, Action<ReceivedMessage<T>> onMessageReceived) where T : class, IMessage, new()
            {
                //
            }
        }
    }

    public static class MessagePipeExtensions
    {
        public static LogMessagePipe WithLog(this IMessagePipe messagePipe, string fromPipe) =>
            new (messagePipe, fromPipe);
    }
}
