using DCL.Multiplayer.Connections.Messaging.Pipe;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Connections.Rooms;
using LiveKit.Internal.FFIClients.Pools;
using LiveKit.Internal.FFIClients.Pools.Memory;

namespace DCL.Multiplayer.Connections.Messaging.Hubs
{
    public class MessagePipesHub : IMessagePipesHub
    {
        private readonly IMessagePipe scenePipe;
        private readonly IMessagePipe islandPipe;

        public MessagePipesHub(IRoomHub roomHub, IMultiPool sendingMultiPool, IMultiPool receivingMultiPool, IMemoryPool memoryPool) : this(
            new MessagePipe(roomHub.SceneRoom().Room().DataPipe, sendingMultiPool, receivingMultiPool, memoryPool, RoomSource.GATEKEEPER)
               .WithLog("Scene"),
            new MessagePipe(roomHub.IslandRoom().DataPipe, sendingMultiPool, receivingMultiPool, memoryPool, RoomSource.ISLAND)
               .WithLog("Island")
        ) { }

        public MessagePipesHub(IMessagePipe scenePipe, IMessagePipe islandPipe)
        {
            this.scenePipe = scenePipe;
            this.islandPipe = islandPipe;
        }

        public IMessagePipe ScenePipe() =>
            scenePipe;

        public IMessagePipe IslandPipe() =>
            islandPipe;

        public void Dispose()
        {
            scenePipe.Dispose();
            islandPipe.Dispose();
        }
    }
}
