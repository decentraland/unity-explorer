using Decentraland.Kernel.Comms.Rfc4;
using ECS.SceneLifeCycle;
using Google.Protobuf;
using LiveKit.Rooms;
using System;

namespace DCL.Multiplayer.Connections.Messaging.Pipe
{
    public interface IMessagePipe : IDisposable
    {
        MessageWrap<T> NewMessage<T>() where T: class, IMessage, new();

        void Subscribe<T>(Packet.MessageOneofCase ofCase, Action<ReceivedMessage<T>> onMessageReceived) where T: class, IMessage, new();

        class Null : IMessagePipe
        {
            private Null() {}

            public static readonly Null INSTANCE = new ();

            public MessageWrap<T> NewMessage<T>() where T: class, IMessage, new() =>
                throw new Exception("Null implementation");

            public void Subscribe<T>(Packet.MessageOneofCase _, Action<ReceivedMessage<T>> __) where T: class, IMessage, new() { }
            public void Dispose() { }
        }
    }

    public static class MessagePipeBuilder
    {
        public static LogMessagePipe WithLog(this IMessagePipe messagePipe, string fromPipe) =>
            new (messagePipe, fromPipe);

        public static InitialSceneSyncMessagePipe WithInitialSceneSync(this IMessagePipe messagePipe, IRoom room, IScenesCache scenesCache) =>
            new (messagePipe, room, scenesCache);
    }
}
