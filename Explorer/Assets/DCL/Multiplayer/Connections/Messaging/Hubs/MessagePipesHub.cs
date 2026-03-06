using DCL.Multiplayer.Connections.Messaging.Pipe;
using DCL.Multiplayer.Connections.Messaging.Throughput;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Connections.Rooms;
using DCL.Multiplayer.Connections.Systems.Throughput;
using LiveKit.Internal.FFIClients.Pools;
using LiveKit.Internal.FFIClients.Pools.Memory;

namespace DCL.Multiplayer.Connections.Messaging.Hubs
{
    public class MessagePipesHub : IMessagePipesHub
    {
        private readonly IMessagePipe scenePipe;
        private readonly IMessagePipe islandPipe;
        private readonly IMessagePipe chatPipe;

        public MessagePipesHub(
            IRoomHub roomHub,
            IMultiPool multiPool,
            IMemoryPool memoryPool,
            ThroughputBufferBunch islandBufferBunch,
            ThroughputBufferBunch sceneBufferBunch,
            ThroughputBufferBunch chatBufferBunch
        ) : this(
            new MessagePipe(roomHub.SceneRoom().Room().DataPipe.WithThroughputMeasure(sceneBufferBunch), multiPool, memoryPool, RoomSource.GATEKEEPER)
               .WithLog("Scene"),
            new MessagePipe(roomHub.IslandRoom().DataPipe.WithThroughputMeasure(islandBufferBunch), multiPool, memoryPool, RoomSource.ISLAND)
               .WithLog("Island"),
            new MessagePipe(roomHub.ChatRoom().DataPipe.WithThroughputMeasure(chatBufferBunch), multiPool, memoryPool, RoomSource.CHAT)
               .WithLog("Chat")
        ) { }

        public MessagePipesHub(IMessagePipe scenePipe, IMessagePipe islandPipe, IMessagePipe chatPipe)
        {
            this.scenePipe = scenePipe;
            this.islandPipe = islandPipe;
            this.chatPipe = chatPipe;
        }

        public IMessagePipe ScenePipe() =>
            scenePipe;

        public IMessagePipe IslandPipe() =>
            islandPipe;

        public IMessagePipe ChatPipe() =>
            chatPipe;

        public void Dispose()
        {
            scenePipe.Dispose();
            islandPipe.Dispose();
            chatPipe.Dispose();
        }
    }
}
