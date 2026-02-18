using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.Archipelago.Rooms.Chat;
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
        private readonly VoiceChatActivatableConnectiveRoom voiceChatRoom;

        private readonly IParticipantsHub islandParticipantsHub;
        private readonly IParticipantsHub sceneParticipantsHub;

        private readonly HashSet<string> identityHashCache = new (32);

        private long participantsUpdateLastFrame = -1;

        public RoomHub(IConnectiveRoom archipelagoIslandRoom, IGateKeeperSceneRoom gateKeeperSceneRoom, IConnectiveRoom chatRoom, VoiceChatActivatableConnectiveRoom voiceChatRoom)
        {
            this.archipelagoIslandRoom = archipelagoIslandRoom;
            this.gateKeeperSceneRoom = gateKeeperSceneRoom;
            this.chatRoom = chatRoom;
            this.voiceChatRoom = voiceChatRoom;

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

        public VoiceChatActivatableConnectiveRoom VoiceChatRoom() =>
            voiceChatRoom;

        /// <summary>
        ///     Starts all rooms except the Voice Chat, as this one only starts when there is a live voice chat going
        /// </summary>
        /// <returns>True if all rooms connected correctly</returns>
        public async UniTask<bool> StartAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            ReportHub.WithReport(ReportCategory.COMMS_SCENE_HANDLER).Log("RoomHub.StartAsync begin");

            UniTask<bool> islandStartTask = archipelagoIslandRoom.StartIfNotAsync();
            UniTask<bool> gateKeeperStartTask = gateKeeperSceneRoom.StartIfNotAsync();
            UniTask<bool> chatStartTask = chatRoom.StartIfNotAsync();

            bool islandStarted = await islandStartTask;
            ReportHub.WithReport(ReportCategory.COMMS_SCENE_HANDLER).Log($"RoomHub.StartAsync island={islandStarted} t={stopwatch.ElapsedMilliseconds}ms {RoomStateSnapshot("island", archipelagoIslandRoom)}");

            bool gateKeeperStarted = await gateKeeperStartTask;
            ReportHub.WithReport(ReportCategory.COMMS_SCENE_HANDLER).Log($"RoomHub.StartAsync gateKeeper={gateKeeperStarted} t={stopwatch.ElapsedMilliseconds}ms {RoomStateSnapshot("gateKeeper", gateKeeperSceneRoom)}");

            bool chatStarted = await chatStartTask;
            ReportHub.WithReport(ReportCategory.COMMS_SCENE_HANDLER).Log($"RoomHub.StartAsync chat={chatStarted} t={stopwatch.ElapsedMilliseconds}ms {RoomStateSnapshot("chat", chatRoom)}");

            bool allStarted = islandStarted && gateKeeperStarted && chatStarted;
            ReportHub.WithReport(ReportCategory.COMMS_SCENE_HANDLER).Log($"RoomHub.StartAsync end allStarted={allStarted} total={stopwatch.ElapsedMilliseconds}ms");

            return allStarted;
        }

        private static string RoomStateSnapshot(string roomName, IConnectiveRoom room) =>
            $"[{roomName}: state={room.CurrentState().ToStringNonAlloc()}, attempt={room.AttemptToConnectState.ToStringNonAlloc()}, health={room.CurrentConnectionLoopHealth.ToStringNonAlloc()}]";

        /// <summary>
        ///     We stop all rooms when logging out as we need to change profiles.
        /// </summary>
        public UniTask StopAsync() =>
            UniTask.WhenAll(
                archipelagoIslandRoom.StopIfNotAsync(),
                gateKeeperSceneRoom.StopIfNotAsync(),
                chatRoom.StopIfNotAsync(),
                voiceChatRoom.StopIfNotAsync()
            );

        /// <summary>
        ///     Stops only local rooms, that is, only the Island Room and Scene Room, as the other rooms are needed for the chat and voice chat
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

            IReadOnlyDictionary<string, Participant> islandIdentities = islandParticipantsHub.RemoteParticipantIdentities();
            IReadOnlyDictionary<string, Participant> sceneIdentities = sceneParticipantsHub.RemoteParticipantIdentities();

            identityHashCache.EnsureCapacity(islandIdentities.Count + sceneIdentities.Count);

            foreach ((string? id, _) in islandIdentities)
                identityHashCache.Add(id);

            foreach ((string? id, _) in sceneIdentities)
                identityHashCache.Add(id);

            participantsUpdateLastFrame = MultithreadingUtility.FrameCount;

            return identityHashCache;
        }
    }
}
