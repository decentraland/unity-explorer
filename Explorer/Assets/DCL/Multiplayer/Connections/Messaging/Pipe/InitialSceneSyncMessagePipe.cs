using Cysharp.Threading.Tasks;
using Decentraland.Kernel.Comms.Rfc4;
using ECS.SceneLifeCycle;
using Google.Protobuf;
using LiveKit.Rooms;
using System;
using System.Threading;
using Utility;

namespace DCL.Multiplayer.Connections.Messaging.Pipe
{
    public class InitialSceneSyncMessagePipe : IMessagePipe
    {
        private readonly IMessagePipe origin;
        private readonly IRoom room;
        private readonly IScenesCache scenesCache;
        private readonly CancellationTokenSource cancellationTokenSource = new ();

        public InitialSceneSyncMessagePipe(IMessagePipe origin, IRoom room, IScenesCache scenesCache)
        {
            room.ConnectionUpdated += RoomOnConnectionUpdated;
            this.origin = origin;
            this.room = room;
            this.scenesCache = scenesCache;
            SendEvent().Forget();
        }

        private async UniTaskVoid SendEvent()
        {
            while (cancellationTokenSource.IsCancellationRequested == false)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(0.5f));
                SyncScene();
            }
        }

        private void SyncScene()
        {
            var message = origin.NewMessage<Scene>();
            message.Payload.SceneId = SceneId();
            ReadOnlySpan<byte> span = stackalloc byte[] { 2, 2 };
            message.Payload.Data = ByteString.CopyFrom(span);
            message.SendAndDisposeAsync(cancellationTokenSource.Token).Forget();
        }

        private void RoomOnConnectionUpdated(IRoom room, ConnectionUpdate connectionupdate)
        {
            SyncScene();
        }

        private string SceneId() =>
            scenesCache?.CurrentScene?.SceneData?.SceneEntityDefinition?.id ?? string.Empty;

        public void Dispose()
        {
            cancellationTokenSource.SafeCancelAndDispose();
            room.ConnectionUpdated -= RoomOnConnectionUpdated;
            origin.Dispose();
        }

        public MessageWrap<T> NewMessage<T>() where T: class, IMessage, new() =>
            origin.NewMessage<T>();

        public void Subscribe<T>(Packet.MessageOneofCase ofCase, Action<ReceivedMessage<T>> onMessageReceived) where T: class, IMessage, new()
        {
            origin.Subscribe(ofCase, onMessageReceived);
        }
    }
}
