using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Archipelago.Rooms.Chat;
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
        private readonly IConnectiveRoom chatRoom;
        private readonly IVoiceChatActivatableConnectiveRoom voiceChatRoom;

        private readonly IParticipantsHub islandParticipantsHub;
        private readonly IParticipantsHub sceneParticipantsHub;

        private readonly HashSet<string> identityHashCache = new (32);

        private long participantsUpdateLastFrame = -1;

        public RoomHub(IConnectiveRoom archipelagoIslandRoom, IGateKeeperSceneRoom gateKeeperSceneRoom, IConnectiveRoom chatRoom, IVoiceChatActivatableConnectiveRoom voiceChatRoom)
        {
            this.archipelagoIslandRoom = archipelagoIslandRoom;
            this.gateKeeperSceneRoom = gateKeeperSceneRoom;
            this.chatRoom = chatRoom;
            this.voiceChatRoom = voiceChatRoom;

            islandParticipantsHub = this.archipelagoIslandRoom.Room().Participants;
            sceneParticipantsHub = this.gateKeeperSceneRoom.Room().Participants;

            AllLocalRoomsRemoteParticipantIdentities();
        }

        public IRoom IslandRoom() => archipelagoIslandRoom.Room();

        public IGateKeeperSceneRoom SceneRoom() => gateKeeperSceneRoom;

        public IRoom ChatRoom() => chatRoom.Room();

        public IVoiceChatActivatableConnectiveRoom VoiceChatRoom() => voiceChatRoom;

        /// <summary>
        /// Starts all rooms except the Voice Chat, as this one only starts when there is a live voice chat going
        /// </summary>
        /// <returns>True if all rooms connected correctly</returns>
        public async UniTask<bool> StartAsync()
        {
            var result = await UniTask.WhenAll(
                archipelagoIslandRoom.StartIfNotAsync(),
                gateKeeperSceneRoom.StartIfNotAsync(),
                chatRoom.StartIfNotAsync());

            return result is { Item1: true, Item2: true, Item3: true };
        }

        /// <summary>
        /// We stop all rooms when logging out as we need to change profiles.
        /// </summary>
        public UniTask StopAsync() =>
            UniTask.WhenAll(
                archipelagoIslandRoom.StopIfNotAsync(),
                gateKeeperSceneRoom.StopIfNotAsync(),
                chatRoom.StopIfNotAsync(),
                voiceChatRoom.StopIfNotAsync()
            );

        /// <summary>
        /// Stops only local rooms, that is, only the Island Room and Scene Room, as the other rooms are needed for the chat and voice chat
        /// </summary>
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
