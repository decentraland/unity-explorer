using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.Multiplayer.Connections.Rooms.Connective;
using LiveKit.Proto;
using LiveKit.Rooms;
using LiveKit.Rooms.Info;
using LiveKit.Rooms.Participants;
using System.Collections.Generic;
using Utility.Multithreading;

namespace DCL.Multiplayer.Connections.RoomHubs
{
    public class RoomHub : IRoomHub
    {
        private readonly IConnectiveRoom archipelagoIslandRoom;
        private readonly IGateKeeperSceneRoom gateKeeperSceneRoom;

        private readonly IParticipantsHub islandParticipantsHub;
        private readonly IParticipantsHub sceneParticipantsHub;

        private readonly HashSet<string> identityHashCache = new (32);
        private readonly IRoomInfo islandRoomInfo;
        private readonly IRoomInfo sceneRoomInfo;

        private long participantsUpdateLastFrame = -1;

        public int ParticipantsCount => AllRoomsRemoteParticipantIdentities().Count;
        public bool HasAnyRoomConnected => islandRoomInfo.ConnectionState == ConnectionState.ConnConnected || sceneRoomInfo.ConnectionState == ConnectionState.ConnConnected;

        public RoomHub(IConnectiveRoom archipelagoIslandRoom, IGateKeeperSceneRoom gateKeeperSceneRoom)
        {
            this.archipelagoIslandRoom = archipelagoIslandRoom;
            this.gateKeeperSceneRoom = gateKeeperSceneRoom;

            islandParticipantsHub = this.archipelagoIslandRoom.Room().Participants;
            sceneParticipantsHub = this.gateKeeperSceneRoom.Room().Participants;

            islandRoomInfo = this.archipelagoIslandRoom.Room().Info;
            sceneRoomInfo = this.gateKeeperSceneRoom.Room().Info;

            AllRoomsRemoteParticipantIdentities();
        }

        public IRoom IslandRoom() =>
            archipelagoIslandRoom.Room();

        public IGateKeeperSceneRoom SceneRoom() =>
            gateKeeperSceneRoom;

        public async UniTask<bool> StartAsync()
        {
            var result = await UniTask.WhenAll(
                archipelagoIslandRoom.StartIfNotAsync(),
                gateKeeperSceneRoom.StartIfNotAsync()
            );

            return result is { Item1: true, Item2: true };
        }

        public UniTask StopAsync() =>
            UniTask.WhenAll(
                archipelagoIslandRoom.StopIfNotAsync(),
                gateKeeperSceneRoom.StopIfNotAsync()
            );

        public HashSet<string> AllRoomsRemoteParticipantIdentities()
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
