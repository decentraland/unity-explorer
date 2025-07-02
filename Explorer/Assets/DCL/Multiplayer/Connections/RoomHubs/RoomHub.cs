using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.Multiplayer.Connections.Rooms.Connective;
using LiveKit.Rooms;
using LiveKit.Rooms.Participants;
using System.Collections.Generic;
using System.Diagnostics;
using Utility.Multithreading;

namespace DCL.Multiplayer.Connections.RoomHubs
{
    public class RoomHub : IRoomHub
    {
        private readonly IConnectiveRoom archipelagoIslandRoom;
        private readonly IGateKeeperSceneRoom gateKeeperSceneRoom;
        private readonly IConnectiveRoom chatRoom;

        private readonly IParticipantsHub islandParticipantsHub;
        private readonly IParticipantsHub sceneParticipantsHub;

        private readonly HashSet<string> identityHashCache = new (32);

        private long participantsUpdateLastFrame = -1;

        public RoomHub(IConnectiveRoom archipelagoIslandRoom, IGateKeeperSceneRoom gateKeeperSceneRoom, IConnectiveRoom chatRoom)
        {
            this.archipelagoIslandRoom = archipelagoIslandRoom;
            this.gateKeeperSceneRoom = gateKeeperSceneRoom;
            this.chatRoom = chatRoom;

            islandParticipantsHub = this.archipelagoIslandRoom.Room().Participants;
            sceneParticipantsHub = this.gateKeeperSceneRoom.Room().Participants;

            AllLocalRoomsRemoteParticipantIdentities();
        }

        public IRoom IslandRoom() =>
            archipelagoIslandRoom.Room();

        public IGateKeeperSceneRoom SceneRoom() =>
            gateKeeperSceneRoom;

        public IRoom ChatRoom() =>
            chatRoom.Room();

        public async UniTask<bool> StartAsync()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            UnityEngine.Debug.Log("JUANI ROOM STARTING");
            var result = await UniTask.WhenAll(
                archipelagoIslandRoom.StartIfNotAsync("ArchipelagoIslandRoom"),
                gateKeeperSceneRoom.StartIfNotAsync("GateKeeperSceneRoom"),
                chatRoom.StartIfNotAsync("ChatRoom")
            );
            stopwatch.Stop();
            UnityEngine.Debug.Log($"JUANI ROOM STARTING ENDED {stopwatch.ElapsedMilliseconds}");


            return result is { Item1: true, Item2: true, Item3: true };
        }

        public UniTask StopAsync() =>
            UniTask.WhenAll(
                archipelagoIslandRoom.StopIfNotAsync(),
                gateKeeperSceneRoom.StopIfNotAsync(),
                chatRoom.StopIfNotAsync()
            );

        public UniTask StopLocalRoomsAsync() =>
            UniTask.WhenAll(
                archipelagoIslandRoom.StopIfNotAsync(),
                gateKeeperSceneRoom.StopIfNotAsync()
            );


        public IReadOnlyCollection<string> AllLocalRoomsRemoteParticipantIdentities()
        {
            if (participantsUpdateLastFrame == MultithreadingUtility.FrameCount)
                return identityHashCache;

            identityHashCache.Clear();

            IReadOnlyCollection<string> islandIdentities = islandParticipantsHub.RemoteParticipantIdentities();
            IReadOnlyCollection<string> sceneIdentities = sceneParticipantsHub.RemoteParticipantIdentities();

            identityHashCache.EnsureCapacity(islandIdentities.Count + sceneIdentities.Count);

            // See: https://github.com/decentraland/unity-explorer/issues/3796
            lock (islandIdentities)
            {
                foreach (string? id in islandIdentities)
                    identityHashCache.Add(id);
            }

            // See: https://github.com/decentraland/unity-explorer/issues/3796
            lock (sceneIdentities)
            {
                foreach (string? id in sceneIdentities)
                    identityHashCache.Add(id);
            }

            participantsUpdateLastFrame = MultithreadingUtility.FrameCount;

            return identityHashCache;
        }
    }
}
