using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.Messaging.Pipe;
using LiveKit.Internal.FFIClients.Pools;
using LiveKit.Internal.FFIClients.Pools.Memory;
using LiveKit.Rooms;

namespace DCL.Multiplayer.Connections.RoomHubs
{
    public class MessagePipedMutableRoomHub : IMutableRoomHub
    {
        private readonly IMutableRoomHub origin;
        private readonly IMutableMessagePipesHub messagePipesHub;
        private readonly IMultiPool multiPool;
        private readonly IMemoryPool memoryPool;

        public MessagePipedMutableRoomHub(IMutableRoomHub origin, IMutableMessagePipesHub messagePipesHub, IMultiPool multiPool, IMemoryPool memoryPool)
        {
            this.origin = origin;
            this.messagePipesHub = messagePipesHub;
            this.multiPool = multiPool;
            this.memoryPool = memoryPool;
        }

        public IRoom IslandRoom() =>
            origin.IslandRoom();

        public IRoom SceneRoom() =>
            origin.SceneRoom();

        public void AssignIslandRoom(IRoom playRoom)
        {
            messagePipesHub.AssignIslandPipe(NewMessagePipe(playRoom));
            origin.AssignIslandRoom(playRoom);
        }

        public void AssignSceneRoom(IRoom playRoom)
        {
            messagePipesHub.AssignScenePipe(NewMessagePipe(playRoom));
            origin.AssignSceneRoom(playRoom);
        }

        private IMessagePipe NewMessagePipe(IRoom room) =>
            new MessagePipe(room.DataPipe, multiPool, memoryPool);
    }
}
