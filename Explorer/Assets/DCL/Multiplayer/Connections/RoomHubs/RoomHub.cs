using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.Multiplayer.Connections.Rooms.Connective;
using LiveKit.Rooms;
using LiveKit.Rooms.Participants;
using System.Collections.Generic;
using Utility.Multithreading;

namespace DCL.Multiplayer.Connections.RoomHubs
{
    public class RoomHub : IRoomHub
    {
        private readonly IConnectiveRoom archipelagoIslandRoom;
        private readonly IGateKeeperSceneRoom gateKeeperSceneRoom;
        private readonly IConnectiveRoom sharedPrivateConversationsRoom;

        private readonly IParticipantsHub islandParticipantsHub;
        private readonly IParticipantsHub sceneParticipantsHub;

        private readonly HashSet<string> identityHashCache = new (32);

        private long participantsUpdateLastFrame = -1;

        public RoomHub(IConnectiveRoom archipelagoIslandRoom, IGateKeeperSceneRoom gateKeeperSceneRoom, IConnectiveRoom sharedPrivateConversationsRoom)
        {
            this.archipelagoIslandRoom = archipelagoIslandRoom;
            this.gateKeeperSceneRoom = gateKeeperSceneRoom;
            this.sharedPrivateConversationsRoom = sharedPrivateConversationsRoom;

            islandParticipantsHub = this.archipelagoIslandRoom.Room().Participants;
            sceneParticipantsHub = this.gateKeeperSceneRoom.Room().Participants;

            AllRoomsRemoteParticipantIdentities();
        }

        public IRoom IslandRoom() =>
            archipelagoIslandRoom.Room();

        public IGateKeeperSceneRoom SceneRoom() =>
            gateKeeperSceneRoom;

        public IRoom SharedPrivateConversationsRoom() =>
            sharedPrivateConversationsRoom.Room();

        public async UniTask<bool> StartAsync()
        {
            var result = await UniTask.WhenAll(
                archipelagoIslandRoom.StartIfNotAsync(),
                gateKeeperSceneRoom.StartIfNotAsync(),
                sharedPrivateConversationsRoom.StartIfNotAsync()
            );

            return result is { Item1: true, Item2: true };
        }

        public UniTask StopAsync() =>
            UniTask.WhenAll(
                archipelagoIslandRoom.StopIfNotAsync(),
                gateKeeperSceneRoom.StopIfNotAsync(),
                sharedPrivateConversationsRoom.StopIfNotAsync()
            );

        public IReadOnlyCollection<string> AllRoomsRemoteParticipantIdentities()
        {
            if (participantsUpdateLastFrame == MultithreadingUtility.FrameCount)
                return identityHashCache;

            identityHashCache.Clear();

            IReadOnlyCollection<string> islandIdentities = islandParticipantsHub.RemoteParticipantIdentities();
            IReadOnlyCollection<string> sceneIdentities = sceneParticipantsHub.RemoteParticipantIdentities();

            identityHashCache.EnsureCapacity(islandIdentities.Count + sceneIdentities.Count);

            foreach (string? id in islandIdentities)
                identityHashCache.Add(id);

            foreach (string? id in sceneIdentities)
                identityHashCache.Add(id);

            participantsUpdateLastFrame = MultithreadingUtility.FrameCount;

            return identityHashCache;
        }
    }
}
