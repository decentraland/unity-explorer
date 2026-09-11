using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Archipelago.Rooms.Chat;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using LiveKit.Proto;
using LiveKit.Rooms;
using LiveKit.Rooms.Participants;
using System.Collections.Generic;
using DCL.LiveKit.Public;

namespace DCL.Multiplayer.Connections.RoomHubs
{
    public interface IRoomHub
    {
        IRoom IslandRoom();
        IGateKeeperSceneRoom SceneRoom();
        IRoom ChatRoom();
        VoiceChatActivatableConnectiveRoom VoiceChatRoom();

        bool TryGetUser(string wallet, out LKParticipant? participant, out IRoom? room);

        UniTask<bool> StartAsync();
        UniTask StopAsync();
        UniTask StopLocalRoomsAsync();

        IReadOnlyCollection<string> AllLocalRoomsRemoteParticipantIdentities();

        /// <summary>
        ///     State of every room <see cref="StartAsync" /> brings up, as <c>Name[state, connect attempt, connection loop]</c>,
        ///     so a failed or timed out start tells which room did not come up. Voice Chat is left out as it is not started there.
        /// </summary>
        string RoomsStateInfo();
    }

    public static class RoomHubExtensions
    {
        public static bool HasAnyRoomConnected(this IRoomHub roomHub) =>
            roomHub.IslandRoom().Info.ConnectionState == LKConnectionState.ConnConnected ||
            roomHub.SceneRoom().Room().Info.ConnectionState == LKConnectionState.ConnConnected;

        public static int ParticipantsCount(this IRoomHub roomHub) =>
            roomHub.AllLocalRoomsRemoteParticipantIdentities().Count;

        /// <summary>
        /// Room used for the video streaming
        /// </summary>
        public static IRoom StreamingRoom(this IRoomHub roomHub) =>
            roomHub.SceneRoom().Room();
    }
}
